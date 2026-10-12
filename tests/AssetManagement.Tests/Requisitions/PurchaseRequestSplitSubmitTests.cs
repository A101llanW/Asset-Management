using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Stage (b) of catalog requisition flows: org policies, group-and-split on submit.</summary>
    [TestFixture]
    public class PurchaseRequestSplitSubmitTests
    {
        private const int LaptopTypeId = 10;
        private const int DeskTypeId = 20;
        private const int LaptopSubTypeId = 100;

#pragma warning disable 618 // test reads through the fake repository
        private static FakeUnitOfWork Seed(string classificationMode = null, string mixedPolicy = null)
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
            uow.Seed(new Role { Id = 8, Name = "Procurement", IsActive = true });
            uow.Seed(new SystemSetting { Id = 1, SettingKey = "Approval.Process.Purchase.Enabled", SettingValue = "true", IsActive = true });
            uow.Seed(new SystemSetting { Id = 2, SettingKey = "Approval.Process.Purchase.StageRoleIds", SettingValue = "9", IsActive = true });
            if (classificationMode != null)
            {
                uow.Seed(new SystemSetting { Id = 3, SettingKey = RequisitionPolicySettings.ClassificationModeKey, SettingValue = classificationMode, IsActive = true });
            }

            if (mixedPolicy != null)
            {
                uow.Seed(new SystemSetting { Id = 4, SettingKey = RequisitionPolicySettings.MixedFlowPolicyKey, SettingValue = mixedPolicy, IsActive = true });
            }

            uow.Seed(new AssetCategory { Id = 1, OrganizationId = 1, Name = "ICT", IsActive = true });
            uow.Seed(new AssetCategory { Id = 2, OrganizationId = 1, Name = "Furniture", IsActive = true });
            uow.Seed(new AssetType { Id = LaptopTypeId, OrganizationId = 1, AssetCategoryId = 1, Name = "Laptops", IsActive = true });
            uow.Seed(new AssetType { Id = DeskTypeId, OrganizationId = 1, AssetCategoryId = 2, Name = "Desks", IsActive = true });
            uow.Seed(new AssetSubType { Id = LaptopSubTypeId, OrganizationId = 1, AssetTypeId = LaptopTypeId, Name = "Latitude 5440", IsActive = true });

            // Laptops go to the ICT Manager (role 7) and Procurement (role 8) raises the PO.
            uow.Seed(new CatalogRequisitionFlow
            {
                Id = 1,
                OrganizationId = 1,
                Scope = CatalogFlowScope.Type,
                ScopedEntityId = LaptopTypeId,
                FlowMode = RequisitionFlowMode.Custom,
                CustomStageRoleIds = "7",
                PurchaseOrderRaiserRoleId = 8,
                IsActive = true
            });
            return uow;
        }

        private static PurchaseRequestService Service(FakeUnitOfWork uow)
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

        private static PurchaseRequestCreateVm Form(params PurchaseRequestLineCreateVm[] lines)
        {
            return new PurchaseRequestCreateVm
            {
                DepartmentId = 5,
                Justification = "Term 3 setup",
                Notes = "Shared note",
                Currency = "KES",
                Lines = lines.ToList()
            };
        }

        private static PurchaseRequestLineCreateVm Line(string description, int quantity, int? typeId = null, int? subTypeId = null)
        {
            return new PurchaseRequestLineCreateVm { Description = description, Quantity = quantity, AssetTypeId = typeId, AssetSubTypeId = subTypeId };
        }

        private static List<PurchaseRequestLine> LinesOf(FakeUnitOfWork uow, int requestId)
        {
            return uow.Repository<PurchaseRequestLine>().GetAll().Where(x => x.PurchaseRequestId == requestId).OrderBy(x => x.LineNumber).ToList();
        }

        [Test]
        public void OptionalMode_NoClassification_SinglePr_SameAsToday()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatch(Form(Line("Plates", 10), Line("Cups", 5)), "requester");

            Assert.IsFalse(result.IsSplit);
            Assert.IsNull(result.GroupId);
            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            Assert.AreEqual("4", pr.ApprovalStageRoleIds);
            Assert.AreEqual(ApprovalStatus.Pending, pr.ApprovalStatus);
            Assert.IsNull(pr.PurchaseRequestGroupId);
            Assert.AreEqual("Custom on Dining", pr.ApprovalFlowSource);
            Assert.AreEqual(15, pr.Quantity);
            Assert.AreEqual("Plates; Cups", pr.ItemDescription);
            Assert.AreEqual(2, LinesOf(uow, pr.Id).Count);
            Assert.AreEqual(0, uow.Repository<PurchaseRequestGroup>().GetAll().Count());
        }

        [Test]
        public void Submit_LegacyIntApi_StillReturnsId()
        {
            var uow = Seed();
            var id = Service(uow).Submit(new PurchaseRequestCreateVm
            {
                DepartmentId = 5,
                ItemDescription = "Kitchen supplies",
                Justification = "Restock",
                Quantity = 2,
                Currency = "KES"
            }, "requester");

            var pr = uow.Repository<PurchaseRequest>().GetById(id);
            Assert.AreEqual("PR-" + id.ToString("D6"), pr.RequestNumber);
            Assert.AreEqual(2, pr.Quantity);
        }

        [Test]
        public void SameCatalogPath_AllLines_SinglePrOnCatalogPath()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatch(Form(Line("Laptop A", 2, LaptopTypeId), Line("Laptop B", 1, null, LaptopSubTypeId)), "requester");

            Assert.IsFalse(result.IsSplit);
            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            Assert.AreEqual("7", pr.ApprovalStageRoleIds);
            Assert.AreEqual("Custom on Type: Laptops", pr.ApprovalFlowSource);
        }

        [Test]
        public void MixedPaths_AutoSplit_TwoPrsOneGroup_LinesNotDuplicated()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatch(Form(
                Line("Laptop", 3, LaptopTypeId),
                Line("Desk", 4, DeskTypeId),
                Line("Spare laptop", 1, LaptopTypeId)), "requester");

            Assert.IsTrue(result.IsSplit);
            Assert.AreEqual(2, result.RequestIds.Count);
            Assert.IsTrue(result.GroupId.HasValue);
            var group = uow.Repository<PurchaseRequestGroup>().GetById(result.GroupId.Value);
            Assert.AreEqual("BATCH-" + group.Id.ToString("D6"), group.BatchLabel);
            Assert.AreEqual(result.BatchLabel, group.BatchLabel);

            var laptops = uow.Repository<PurchaseRequest>().GetById(result.RequestIds[0]);
            var desks = uow.Repository<PurchaseRequest>().GetById(result.RequestIds[1]);
            Assert.AreEqual(group.Id, laptops.PurchaseRequestGroupId);
            Assert.AreEqual(group.Id, desks.PurchaseRequestGroupId);
            Assert.AreNotEqual(laptops.RequestNumber, desks.RequestNumber);

            Assert.AreEqual("7", laptops.ApprovalStageRoleIds);
            Assert.AreEqual("4", desks.ApprovalStageRoleIds);
            Assert.AreEqual(4, laptops.Quantity);
            Assert.AreEqual("Laptop; Spare laptop", laptops.ItemDescription);
            Assert.AreEqual(4, desks.Quantity);
            Assert.AreEqual("Desk", desks.ItemDescription);

            // Shared metadata
            Assert.AreEqual(laptops.Justification, desks.Justification);
            Assert.AreEqual(laptops.DepartmentId, desks.DepartmentId);
            Assert.AreEqual("Shared note", desks.Notes);

            // Lines partitioned, renumbered per requisition, classification stored, nothing duplicated.
            var laptopLines = LinesOf(uow, laptops.Id);
            var deskLines = LinesOf(uow, desks.Id);
            Assert.AreEqual(2, laptopLines.Count);
            Assert.AreEqual(1, deskLines.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, laptopLines.Select(x => x.LineNumber).ToArray());
            Assert.AreEqual(DeskTypeId, deskLines[0].AssetTypeId);
            Assert.AreEqual(2, deskLines[0].AssetCategoryId);
            Assert.AreEqual(3, uow.Repository<PurchaseRequestLine>().GetAll().Count());
        }

        [Test]
        public void MixedPaths_Block_Rejected_NothingCreated()
        {
            var uow = Seed(mixedPolicy: "Block");
            var ex = Assert.Throws<BusinessException>(() => Service(uow).SubmitBatch(Form(
                Line("Laptop", 1, LaptopTypeId),
                Line("Desk", 1, DeskTypeId)), "requester"));

            StringAssert.Contains("different approval paths", ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());
            Assert.AreEqual(0, uow.Repository<PurchaseRequestGroup>().GetAll().Count());
        }

        [Test]
        public void Block_SamePath_StillSubmits()
        {
            var uow = Seed(mixedPolicy: "Block");
            var result = Service(uow).SubmitBatch(Form(Line("Desk", 1, DeskTypeId), Line("Chair", 1)), "requester");
            Assert.IsFalse(result.IsSplit);
        }

        [Test]
        public void RequiredPerLine_MissingTypeOnLine2_Fails()
        {
            var uow = Seed(classificationMode: "RequiredPerLine");
            var ex = Assert.Throws<BusinessException>(() => Service(uow).SubmitBatch(Form(
                Line("Laptop", 1, LaptopTypeId),
                Line("Something", 1)), "requester"));

            StringAssert.StartsWith("Line 2", ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());
        }

        [Test]
        public void RequiredPerLine_CategoryAloneIsNotEnough()
        {
            var uow = Seed(classificationMode: "RequiredPerLine");
            var form = Form(new PurchaseRequestLineCreateVm { Description = "Desk", Quantity = 1, AssetCategoryId = 2 });
            Assert.Throws<BusinessException>(() => Service(uow).SubmitBatch(form, "requester"));
        }

        [Test]
        public void RequiredHeader_LinesInheritHeader()
        {
            var uow = Seed(classificationMode: "RequiredHeader");
            var form = Form(Line("Laptop 14in", 1), Line("Laptop 15in", 2));
            form.HeaderAssetTypeId = LaptopTypeId;

            var result = Service(uow).SubmitBatch(form, "requester");

            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            Assert.AreEqual("7", pr.ApprovalStageRoleIds);
            Assert.IsTrue(LinesOf(uow, pr.Id).All(x => x.AssetTypeId == LaptopTypeId && x.AssetCategoryId == 1));
        }

        [Test]
        public void RequiredHeader_MissingHeader_Fails()
        {
            var uow = Seed(classificationMode: "RequiredHeader");
            Assert.Throws<BusinessException>(() => Service(uow).SubmitBatch(Form(Line("Laptop", 1)), "requester"));
        }

        [Test]
        public void SubTypeOnly_IsNormalizedOnTheStoredLine()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatch(Form(Line("Latitude", 1, null, LaptopSubTypeId)), "requester");
            var line = LinesOf(uow, result.PrimaryId).Single();
            Assert.AreEqual(LaptopSubTypeId, line.AssetSubTypeId);
            Assert.AreEqual(LaptopTypeId, line.AssetTypeId);
            Assert.AreEqual(1, line.AssetCategoryId);
        }

        [Test]
        public void ForgedForeignType_IsRejected()
        {
            var uow = Seed();
            uow.Seed(new AssetType { Id = 99, OrganizationId = 2, AssetCategoryId = 1, Name = "Other org", IsActive = true });
            Assert.Throws<BusinessException>(() => Service(uow).SubmitBatch(Form(Line("x", 1, 99)), "requester"));
        }

        [Test]
        public void TargetAsset_PrefillsFirstLineClassification()
        {
            var uow = Seed();
            uow.Seed(new Asset { Id = 500, OrganizationId = 1, AssetTypeId = LaptopTypeId, AssetSubTypeId = LaptopSubTypeId, IsActive = true });
            var form = Form(Line("Replacement battery", 1), Line("Chair", 1));
            form.TargetAssetId = 500;

            var result = Service(uow).SubmitBatch(form, "requester");

            Assert.IsTrue(result.IsSplit);
            var first = uow.Repository<PurchaseRequest>().GetById(result.RequestIds[0]);
            var second = uow.Repository<PurchaseRequest>().GetById(result.RequestIds[1]);
            Assert.AreEqual(500, first.TargetAssetId);
            Assert.IsNull(second.TargetAssetId);
            Assert.AreEqual("7", first.ApprovalStageRoleIds);
            Assert.IsNull(LinesOf(uow, second.Id).Single().AssetTypeId);
        }

        [Test]
        public void PoRaiser_FollowsCatalogSource_ForCatalogPr_AndDepartmentOtherwise()
        {
            var uow = Seed();
            var result = Service(uow).SubmitBatch(Form(Line("Laptop", 1, LaptopTypeId), Line("Desk", 1, DeskTypeId)), "requester");

            var laptops = uow.Repository<PurchaseRequest>().GetById(result.RequestIds[0]);
            var desks = uow.Repository<PurchaseRequest>().GetById(result.RequestIds[1]);
            Assert.AreEqual(8, PurchaseOrderRaiserRules.ResolveForRequest(uow, laptops).PurchaseOrderRaiserRoleId);
            Assert.IsNull(PurchaseOrderRaiserRules.ResolveForRequest(uow, desks).PurchaseOrderRaiserRoleId);
        }

        [Test]
        public void CatalogAutoApprove_SplitPrIsApprovedImmediately()
        {
            var uow = Seed();
            uow.Seed(new CatalogRequisitionFlow
            {
                Id = 2,
                OrganizationId = 1,
                Scope = CatalogFlowScope.Category,
                ScopedEntityId = 2,
                FlowMode = RequisitionFlowMode.AutoApprove,
                IsActive = true
            });

            var result = Service(uow).SubmitBatch(Form(Line("Laptop", 1, LaptopTypeId), Line("Desk", 1, DeskTypeId)), "requester");

            var desks = uow.Repository<PurchaseRequest>().GetById(result.RequestIds[1]);
            Assert.AreEqual(ApprovalStatus.Approved, desks.ApprovalStatus);
            Assert.AreEqual("Auto-approve on Category: Furniture", desks.ApprovalFlowSource);
        }
#pragma warning restore 618
    }
}
