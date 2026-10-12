using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>Stage (c) of catalog requisition flows: services behind the form pickers, the catalog-node approval
    /// panel (Asset Categories / Types / Sub-types Edit), the policy settings, the path preview and the batch page.</summary>
    [TestFixture]
    public class CatalogRequisitionUiTests
    {
        private const int LaptopTypeId = 10;
        private const int DeskTypeId = 20;
        private const int LaptopSubTypeId = 100;

        private sealed class RecordingAuditWriter : IAuditWriter
        {
            public readonly List<string> Entries = new List<string>();

            public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
            {
                Entries.Add(action + "|" + entityId + "|" + oldValues + "|" + newValues);
            }
        }

#pragma warning disable 618 // test reads through the fake repository
        private static FakeUnitOfWork Seed(bool withLaptopFlow = true)
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department
            {
                Id = 5,
                OrganizationId = 1,
                Name = "Dining",
                Code = "DINING",
                IsActive = true,
                IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "4"
            });
            uow.Seed(new Role { Id = 4, Name = "Facilities Manager", IsActive = true });
            uow.Seed(new Role { Id = 7, Name = "ICT Manager", IsActive = true });
            uow.Seed(new SystemSetting { Id = 1, SettingKey = "Approval.Purchase.Enabled", SettingValue = "true", IsActive = true });
            uow.Seed(new SystemSetting { Id = 2, SettingKey = "Approval.Purchase.StageRoleIds", SettingValue = "9", IsActive = true });
            uow.Seed(new AssetCategory { Id = 1, OrganizationId = 1, Name = "ICT", IsActive = true });
            uow.Seed(new AssetCategory { Id = 2, OrganizationId = 1, Name = "Furniture", IsActive = true });
            uow.Seed(new AssetCategory { Id = 3, OrganizationId = 1, Name = "Retired", IsActive = false });
            uow.Seed(new AssetCategory { Id = 50, OrganizationId = 2, Name = "Other org", IsActive = true });
            uow.Seed(new AssetType { Id = LaptopTypeId, OrganizationId = 1, AssetCategoryId = 1, Name = "Laptops", IsActive = true });
            uow.Seed(new AssetType { Id = DeskTypeId, OrganizationId = 1, AssetCategoryId = 2, Name = "Desks", IsActive = true });
            uow.Seed(new AssetType { Id = 30, OrganizationId = 1, AssetCategoryId = 3, Name = "Under retired", IsActive = true });
            uow.Seed(new AssetSubType { Id = LaptopSubTypeId, OrganizationId = 1, AssetTypeId = LaptopTypeId, Name = "Latitude 5440", IsActive = true });
            if (withLaptopFlow)
            {
                uow.Seed(new CatalogRequisitionFlow
                {
                    Id = 1,
                    OrganizationId = 1,
                    Scope = CatalogFlowScope.Type,
                    ScopedEntityId = LaptopTypeId,
                    FlowMode = RequisitionFlowMode.Custom,
                    CustomStageRoleIds = "7",
                    IsActive = true
                });
            }

            return uow;
        }

        private static CatalogRequisitionFlowService FlowService(FakeUnitOfWork uow, IAuditWriter audit = null, int organizationId = 1)
        {
            return new CatalogRequisitionFlowService(uow, audit ?? new NoOpAuditWriter(), new FakeOrganizationScopeService(organizationId: organizationId));
        }

        private static PurchaseRequestService RequestService(FakeUnitOfWork uow)
        {
            return new PurchaseRequestService(
                uow,
                new NoOpAuditWriter(),
                new FakeUserService(),
                new NoOpDepartmentScopeService(),
                new FakeOrganizationScopeService(organizationId: 1),
                new NoOpOutboxWriter(),
                new NoOpWebhookService(),
                new FakeApprovalWorkflowEngine(uow, new NoOpAuditWriter()),
                new FakeOperationsQueryRepository());
        }

        private static PurchaseRequestLineCreateVm Line(string description, int? typeId = null, int? subTypeId = null)
        {
            return new PurchaseRequestLineCreateVm { Description = description, Quantity = 1, AssetTypeId = typeId, AssetSubTypeId = subTypeId };
        }

        private static PurchaseRequestCreateVm Form(params PurchaseRequestLineCreateVm[] lines)
        {
            return new PurchaseRequestCreateVm { DepartmentId = 5, Justification = "Term 3", Currency = "KES", Lines = lines.ToList() };
        }

        private static CatalogRequisitionFlowEditVm Custom(CatalogFlowScope scope, int id, params int[] roleIds)
        {
            return new CatalogRequisitionFlowEditVm
            {
                Scope = scope,
                ScopedEntityId = id,
                FlowMode = RequisitionFlowMode.Custom,
                Stages = roleIds.Select(x => new ApprovalStageSettingsVm { RoleId = x }).ToList()
            };
        }

        // ---- pickers -------------------------------------------------------------------------------------------

        [Test]
        public void CatalogPicker_ActiveOrgNodesOnly_ChildrenOfInactiveParentsHidden()
        {
            var picker = FlowService(Seed()).GetCatalogPicker();

            CollectionAssert.AreEquivalent(new[] { 1, 2 }, picker.Categories.Select(x => x.Id).ToArray());
            CollectionAssert.AreEquivalent(new[] { LaptopTypeId, DeskTypeId }, picker.Types.Select(x => x.Id).ToArray());
            Assert.AreEqual(1, picker.Types.Single(x => x.Id == LaptopTypeId).ParentId);
            Assert.AreEqual(LaptopTypeId, picker.SubTypes.Single().ParentId);
        }

        // ---- catalog node approval panel --------------------------------------------------------------------------

        [Test]
        public void GetFlowForEdit_ExistingCustom_LoadsStagesAndPath()
        {
            var model = FlowService(Seed()).GetFlowForEdit(CatalogFlowScope.Type, LaptopTypeId);

            Assert.IsTrue(model.Exists);
            Assert.AreEqual(RequisitionFlowMode.Custom, model.FlowMode);
            Assert.AreEqual("ICT > Laptops", model.PathLabel);
            Assert.AreEqual(7, model.Stages.Single().RoleId);
            CollectionAssert.AreEqual(new[] { 7 }, model.StoredStageRoleIds.ToArray());
        }

        [Test]
        public void GetFlowForEdit_SubTypeUnderConfiguredType_ShowsInheritedSource()
        {
            var model = FlowService(Seed()).GetFlowForEdit(CatalogFlowScope.SubType, LaptopSubTypeId);

            Assert.IsFalse(model.Exists);
            Assert.AreEqual(RequisitionFlowMode.InheritParent, model.FlowMode);
            Assert.AreEqual("ICT > Laptops > Latitude 5440", model.PathLabel);
            Assert.AreEqual("Custom on Type: Laptops", model.InheritedFromLabel);
            Assert.AreEqual(1, model.Stages.Count, "one blank stage row for the matrix");
        }

        [Test]
        public void GetFlowForEdit_NoAncestorFlow_FallsBackToDepartmentLabel()
        {
            var model = FlowService(Seed()).GetFlowForEdit(CatalogFlowScope.Type, DeskTypeId);
            Assert.AreEqual(CatalogRequisitionFlowService.DepartmentFallbackLabel, model.InheritedFromLabel);
        }

        [Test]
        public void GetFlowForEdit_OtherOrgNode_ReturnsNull()
        {
            Assert.IsNull(FlowService(Seed()).GetFlowForEdit(CatalogFlowScope.Category, 50));
        }

        [Test]
        public void SaveFlow_NewCustom_InsertsRowAndAudits()
        {
            var uow = Seed();
            var audit = new RecordingAuditWriter();
            var model = Custom(CatalogFlowScope.Category, 2, 4, 7);
            model.Stages[1].UserId = " user-7 ";
            model.PurchaseOrderRaiserRoleId = 4;

            FlowService(uow, audit).SaveFlow(model);

            var row = uow.Repository<CatalogRequisitionFlow>().GetAll().Single(x => x.Scope == CatalogFlowScope.Category && x.ScopedEntityId == 2);
            Assert.AreEqual(RequisitionFlowMode.Custom, row.FlowMode);
            Assert.AreEqual(1, row.OrganizationId);
            Assert.AreEqual("4,7", row.CustomStageRoleIds);
            StringAssert.Contains("user-7", row.CustomStageUserIds);
            Assert.AreEqual(4, row.PurchaseOrderRaiserRoleId);
            Assert.AreEqual(1, audit.Entries.Count);
            StringAssert.StartsWith(CatalogRequisitionFlowService.AuditAction + "|Category:2||Mode=Custom", audit.Entries[0]);
        }

        [Test]
        public void SaveFlow_BlankRowsDropped_UserWithoutRoleRejected()
        {
            var uow = Seed();
            var model = Custom(CatalogFlowScope.Category, 2, 4);
            model.Stages.Add(new ApprovalStageSettingsVm());
            FlowService(uow).SaveFlow(model);
            Assert.AreEqual("4", uow.Repository<CatalogRequisitionFlow>().GetAll().Single(x => x.ScopedEntityId == 2 && x.Scope == CatalogFlowScope.Category).CustomStageRoleIds);

            var bad = Custom(CatalogFlowScope.Category, 1, 4);
            bad.Stages.Add(new ApprovalStageSettingsVm { UserId = "someone" });
            Assert.Throws<BusinessException>(() => FlowService(uow).SaveFlow(bad));
        }

        [Test]
        public void SaveFlow_CustomWithoutRoles_Rejected()
        {
            Assert.Throws<BusinessException>(() => FlowService(Seed()).SaveFlow(Custom(CatalogFlowScope.Category, 1)));
        }

        [Test]
        public void SaveFlow_TooManyStages_Rejected()
        {
            var roles = Enumerable.Repeat(4, ApprovalWorkflowSettingsHelper.MaxApprovalStages + 1).ToArray();
            Assert.Throws<BusinessException>(() => FlowService(Seed()).SaveFlow(Custom(CatalogFlowScope.Category, 1, roles)));
        }

        [Test]
        public void SaveFlow_OtherOrgNode_Rejected()
        {
            Assert.Throws<BusinessException>(() => FlowService(Seed()).SaveFlow(Custom(CatalogFlowScope.Category, 50, 4)));
        }

        [Test]
        public void SaveFlow_AutoApprove_ClearsStagesKeepsRaiser()
        {
            var uow = Seed();
            FlowService(uow).SaveFlow(new CatalogRequisitionFlowEditVm
            {
                Scope = CatalogFlowScope.Type,
                ScopedEntityId = LaptopTypeId,
                FlowMode = RequisitionFlowMode.AutoApprove,
                Stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = 7 } },
                PurchaseOrderRaiserRoleId = 4
            });

            var row = uow.Repository<CatalogRequisitionFlow>().GetById(1);
            Assert.AreEqual(RequisitionFlowMode.AutoApprove, row.FlowMode);
            Assert.IsNull(row.CustomStageRoleIds);
            Assert.AreEqual(4, row.PurchaseOrderRaiserRoleId);
        }

        [Test]
        public void SaveFlow_InheritOnExisting_KeepsRowAsInheritAndClearsEverything()
        {
            var uow = Seed();
            FlowService(uow).SaveFlow(new CatalogRequisitionFlowEditVm
            {
                Scope = CatalogFlowScope.Type,
                ScopedEntityId = LaptopTypeId,
                FlowMode = RequisitionFlowMode.InheritParent,
                PurchaseOrderRaiserRoleId = 4
            });

            var row = uow.Repository<CatalogRequisitionFlow>().GetById(1);
            Assert.AreEqual(RequisitionFlowMode.InheritParent, row.FlowMode);
            Assert.IsNull(row.CustomStageRoleIds);
            Assert.IsNull(row.PurchaseOrderRaiserRoleId);
            Assert.AreEqual(0, FlowService(uow).GetConfiguredFlows().Count);
        }

        [Test]
        public void SaveFlow_InheritWithoutRow_WritesNothing()
        {
            var uow = Seed();
            var audit = new RecordingAuditWriter();
            FlowService(uow, audit).SaveFlow(new CatalogRequisitionFlowEditVm { Scope = CatalogFlowScope.Category, ScopedEntityId = 2, FlowMode = RequisitionFlowMode.InheritParent });

            Assert.AreEqual(1, uow.Repository<CatalogRequisitionFlow>().GetAll().Count());
            Assert.AreEqual(0, audit.Entries.Count);
        }

        [Test]
        public void SaveFlow_Unchanged_NoAudit()
        {
            var uow = Seed();
            var audit = new RecordingAuditWriter();
            FlowService(uow, audit).SaveFlow(Custom(CatalogFlowScope.Type, LaptopTypeId, 7));
            Assert.AreEqual(0, audit.Entries.Count);
        }

        [Test]
        public void SaveFlow_ThenSubmit_NewPathUsed()
        {
            var uow = Seed();
            FlowService(uow).SaveFlow(Custom(CatalogFlowScope.Category, 2, 4, 7));

            var result = RequestService(uow).SubmitBatch(Form(Line("Desk", DeskTypeId)), "requester");
            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            Assert.AreEqual("4,7", pr.ApprovalStageRoleIds);
            Assert.AreEqual("Custom on Category: Furniture", pr.ApprovalFlowSource);
        }

        [Test]
        public void GetConfiguredFlows_ListsOverridesWithPaths()
        {
            var uow = Seed();
            FlowService(uow).SaveFlow(new CatalogRequisitionFlowEditVm { Scope = CatalogFlowScope.SubType, ScopedEntityId = LaptopSubTypeId, FlowMode = RequisitionFlowMode.AutoApprove });

            var flows = FlowService(uow).GetConfiguredFlows();
            CollectionAssert.AreEqual(new[] { "ICT > Laptops", "ICT > Laptops > Latitude 5440" }, flows.Select(x => x.PathLabel).ToArray());
        }

        // ---- policies (lazy seeding) ------------------------------------------------------------------------------

        [Test]
        public void Policies_DefaultsWhenNoRows()
        {
            var policies = FlowService(Seed()).GetPolicies();
            Assert.AreEqual(RequisitionClassificationMode.Optional, policies.ClassificationMode);
            Assert.AreEqual(RequisitionMixedFlowPolicy.AutoSplit, policies.MixedFlowPolicy);
            Assert.IsFalse(policies.IsStored);
        }

        [Test]
        public void SavePolicies_FirstSaveSeedsBothRows_SecondSaveUpdates()
        {
            var uow = Seed();
            var audit = new RecordingAuditWriter();
            FlowService(uow, audit).SavePolicies(new RequisitionPolicySettingsVm { ClassificationMode = RequisitionClassificationMode.RequiredPerLine, MixedFlowPolicy = RequisitionMixedFlowPolicy.AutoSplit });

            var rows = uow.Repository<SystemSetting>().GetAll().Where(x => x.SettingKey.StartsWith("Purchases.Requisition.")).ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.IsTrue(rows.All(x => x.OrganizationId == 1));
            Assert.AreEqual("RequiredPerLine", rows.Single(x => x.SettingKey == RequisitionPolicySettings.ClassificationModeKey).SettingValue);
            Assert.AreEqual("AutoSplit", rows.Single(x => x.SettingKey == RequisitionPolicySettings.MixedFlowPolicyKey).SettingValue);
            Assert.AreEqual(RequisitionClassificationMode.RequiredPerLine, RequisitionPolicySettings.GetClassificationMode(uow));

            FlowService(uow, audit).SavePolicies(new RequisitionPolicySettingsVm { ClassificationMode = RequisitionClassificationMode.RequiredPerLine, MixedFlowPolicy = RequisitionMixedFlowPolicy.Block });
            Assert.AreEqual(2, uow.Repository<SystemSetting>().GetAll().Count(x => x.SettingKey.StartsWith("Purchases.Requisition.")));
            Assert.AreEqual(RequisitionMixedFlowPolicy.Block, RequisitionPolicySettings.GetMixedFlowPolicy(uow));
            Assert.AreEqual(2, audit.Entries.Count);
            Assert.IsTrue(FlowService(uow).GetPolicies().IsStored);
        }

        // ---- create form preview -----------------------------------------------------------------------------------

        [Test]
        public void Preview_MixedPaths_GroupsMatchSubmitPartitions()
        {
            var uow = Seed();
            var form = Form(Line("Laptop", LaptopTypeId), Line("Desk", DeskTypeId), Line("Spare", null, LaptopSubTypeId));
            var preview = RequestService(uow).PreviewPaths(form);

            Assert.IsNull(preview.Error);
            Assert.IsTrue(preview.IsMixed);
            Assert.IsFalse(preview.WillBlock);
            Assert.AreEqual(2, preview.Groups.Count);
            CollectionAssert.AreEqual(new[] { 1, 3 }, preview.Groups[0].LineNumbers.ToArray());
            Assert.AreEqual("Custom on Type: Laptops", preview.Groups[0].SourceLabel);
            Assert.AreEqual("Custom on Dining", preview.Groups[1].SourceLabel);
            Assert.IsNull(preview.Groups[0].SourceKind, "fingerprint is internal only");
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count(), "preview saves nothing");

            var submitted = RequestService(uow).SubmitBatch(form, "requester");
            Assert.AreEqual(preview.Groups.Count, submitted.RequestIds.Count);
        }

        [Test]
        public void Preview_BlockPolicy_FlagsWillBlock()
        {
            var uow = Seed();
            uow.Seed(new SystemSetting { Id = 20, SettingKey = RequisitionPolicySettings.MixedFlowPolicyKey, SettingValue = "Block", IsActive = true });
            var preview = RequestService(uow).PreviewPaths(Form(Line("Laptop", LaptopTypeId), Line("Desk", DeskTypeId)));
            Assert.IsTrue(preview.WillBlock);
        }

        [Test]
        public void Preview_BlankRowsKeepLineNumbers()
        {
            var preview = RequestService(Seed()).PreviewPaths(Form(Line("", LaptopTypeId), Line("Desk", DeskTypeId)));
            CollectionAssert.AreEqual(new[] { 1, 2 }, preview.Lines.Select(x => x.LineNumber).ToArray());
            Assert.AreEqual("Custom on Type: Laptops", preview.Lines[0].SourceLabel);
        }

        [Test]
        public void Preview_RequiredPerLine_ReportsSubmitError()
        {
            var uow = Seed();
            uow.Seed(new SystemSetting { Id = 21, SettingKey = RequisitionPolicySettings.ClassificationModeKey, SettingValue = "RequiredPerLine", IsActive = true });
            var preview = RequestService(uow).PreviewPaths(Form(Line("Laptop", LaptopTypeId), Line("Plates")));
            StringAssert.Contains("Line 2", preview.Error);
            Assert.AreEqual(0, preview.Groups.Count);
        }

        // ---- batch page + details ----------------------------------------------------------------------------------

        [Test]
        public void GetBatch_ListsSplitRequisitions()
        {
            var uow = Seed();
            var service = RequestService(uow);
            var submitted = service.SubmitBatch(Form(Line("Laptop", LaptopTypeId), Line("Desk", DeskTypeId), Line("Chair", DeskTypeId)), "requester");

            var batch = service.GetBatch(submitted.GroupId.Value);
            Assert.AreEqual(submitted.BatchLabel, batch.BatchLabel);
            Assert.AreEqual(2, batch.Requests.Count);
            Assert.AreEqual(0, batch.HiddenCount);
            Assert.AreEqual("Custom on Type: Laptops", batch.Requests[0].ApprovalFlowSource);
            Assert.AreEqual(2, batch.Requests[1].LineCount);
            Assert.AreEqual(2, batch.Requests[1].TotalQuantity);
            Assert.AreEqual(1, batch.Requests[1].StageCount);
            Assert.AreEqual("Dining", batch.Requests[1].DepartmentName);
        }

        [Test]
        public void GetBatch_UnknownGroup_Null()
        {
            Assert.IsNull(RequestService(Seed()).GetBatch(999));
        }

        [Test]
        public void GetById_SplitRequisition_ShowsBatchSiblingsAndClassification()
        {
            var uow = Seed();
            var service = RequestService(uow);
            var submitted = service.SubmitBatch(Form(Line("Laptop", null, LaptopSubTypeId), Line("Desk", DeskTypeId)), "requester");

            var detail = service.GetById(submitted.RequestIds[0]);
            Assert.AreEqual(submitted.BatchLabel, detail.BatchLabel);
            Assert.AreEqual(submitted.RequestIds[1], detail.Siblings.Single().Id);
            Assert.AreEqual("ICT > Laptops > Latitude 5440", detail.LineItems.Single().ClassificationLabel);
        }
#pragma warning restore 618
    }
}
