using System;
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
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Requisitions
{
    /// <summary>
    /// Migration 108: requisition line "Item / asset name" + per-line tag, carried requisition -> PO -> receiving,
    /// and the AssetService name guard. Rows from before 108 (NULL ItemName, no line classification) are never
    /// re-validated and keep the old name fallback chain.
    /// </summary>
    [TestFixture]
    public partial class RequisitionLineAssetNameTests
    {
        private const int LaptopTypeId = 10;
        private const int DeskTypeId = 20;
        private const int LaptopSubTypeId = 100;
        private const int DeskSubTypeId = 200;
        private const int TaggedAssetId = 500;

#pragma warning disable 618 // test reads through the fake repository

        // ---------------- requisition submit ----------------

        private static FakeUnitOfWork SeedRequisitionSetup()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department
            {
                Id = 5, OrganizationId = 1, Name = "Dining", Code = "DINING", IsActive = true, IsRequisitionTarget = true,
                RequisitionFlowMode = RequisitionFlowMode.Custom, CustomStageRoleIds = "4"
            });
            uow.Seed(new Role { Id = 4, Name = "Facilities Manager", IsActive = true });
            uow.Seed(new Role { Id = 7, Name = "ICT Manager", IsActive = true });
            uow.Seed(new SystemSetting { Id = 1, SettingKey = "Approval.Process.Purchase.Enabled", SettingValue = "true", IsActive = true });
            uow.Seed(new SystemSetting { Id = 2, SettingKey = "Approval.Process.Purchase.StageRoleIds", SettingValue = "9", IsActive = true });
            uow.Seed(new AssetCategory { Id = 1, OrganizationId = 1, Name = "ICT", IsActive = true });
            uow.Seed(new AssetCategory { Id = 2, OrganizationId = 1, Name = "Furniture", IsActive = true });
            uow.Seed(new AssetType { Id = LaptopTypeId, OrganizationId = 1, AssetCategoryId = 1, Name = "Laptops", IsActive = true });
            uow.Seed(new AssetType { Id = DeskTypeId, OrganizationId = 1, AssetCategoryId = 2, Name = "Desks", IsActive = true });
            uow.Seed(new AssetSubType { Id = LaptopSubTypeId, OrganizationId = 1, AssetTypeId = LaptopTypeId, Name = "Latitude 5440", IsActive = true });
            uow.Seed(new AssetSubType { Id = DeskSubTypeId, OrganizationId = 1, AssetTypeId = DeskTypeId, Name = "Office desk", IsActive = true });
            uow.Seed(new Asset
            {
                Id = TaggedAssetId, OrganizationId = 1, AssetTag = "ICT-500", AssetName = "Lab laptop 7", CategoryId = 1,
                AssetTypeId = LaptopTypeId, AssetSubTypeId = LaptopSubTypeId, CurrentStatus = AssetStatus.InStore, IsActive = true
            });
            uow.Seed(new CatalogRequisitionFlow
            {
                Id = 1, OrganizationId = 1, Scope = CatalogFlowScope.Type, ScopedEntityId = LaptopTypeId,
                FlowMode = RequisitionFlowMode.Custom, CustomStageRoleIds = "7", IsActive = true
            });
            return uow;
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

        private static PurchaseRequestCreateVm Form(params PurchaseRequestLineCreateVm[] lines)
        {
            return new PurchaseRequestCreateVm { DepartmentId = 5, Justification = "Term 3", Currency = "KES", Lines = lines.ToList() };
        }

        private static PurchaseRequestLineCreateVm NewLine(string name, string description, int? typeId, int? subTypeId, int? categoryId = null)
        {
            return new PurchaseRequestLineCreateVm
            {
                ItemName = name, Description = description, Quantity = 1, AssetCategoryId = categoryId, AssetTypeId = typeId, AssetSubTypeId = subTypeId
            };
        }

        private static PurchaseRequestLineCreateVm TaggedLine(string name, string description)
        {
            return new PurchaseRequestLineCreateVm { ItemName = name, Description = description, Quantity = 1, TargetAssetId = TaggedAssetId };
        }

        private static List<PurchaseRequestLine> LinesOf(FakeUnitOfWork uow, int requestId)
        {
            return uow.Repository<PurchaseRequestLine>().GetAll().Where(x => x.PurchaseRequestId == requestId).OrderBy(x => x.LineNumber).ToList();
        }

        [Test]
        public void Submit_UntaggedLineWithoutName_IsRejected_NothingSaved()
        {
            var uow = SeedRequisitionSetup();
            var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(
                Form(NewLine("Desk", "Office desk", DeskTypeId, DeskSubTypeId), NewLine("   ", "Second desk", DeskTypeId, DeskSubTypeId)), "requester"));
            Assert.AreEqual("Line 2: enter the item / asset name.", ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());
        }

        [Test]
        public void Submit_UntaggedLineNeedsTypeAndSubType()
        {
            var uow = SeedRequisitionSetup();
            var none = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(Form(NewLine("Plates", "Plates", null, null)), "requester"));
            Assert.AreEqual("Line 1: choose the asset type and sub-type.", none.Message);
            var typeOnly = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(Form(NewLine("Desk", "Desk", DeskTypeId, null)), "requester"));
            Assert.AreEqual("Line 1: choose the asset type and sub-type.", typeOnly.Message);
            var categoryOnly = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(Form(NewLine("Desk", "Desk", null, null, categoryId: 2)), "requester"));
            StringAssert.StartsWith("Line 1", categoryOnly.Message);
        }

        [Test]
        public void Submit_NameLongerThanTheLimit_IsRejected()
        {
            var uow = SeedRequisitionSetup();
            var tooLong = new string('a', AssetNameRules.MaxLength + 1);
            var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(Form(NewLine(tooLong, "Desk", DeskTypeId, DeskSubTypeId)), "requester"));
            StringAssert.Contains("cannot be longer than " + AssetNameRules.MaxLength, ex.Message);

            var atLimit = new string('b', AssetNameRules.MaxLength);
            var ok = RequestService(uow).SubmitBatch(Form(NewLine(atLimit, "Desk", DeskTypeId, DeskSubTypeId)), "requester");
            Assert.AreEqual(atLimit, LinesOf(uow, ok.PrimaryId).Single().ItemName);
        }

        [Test]
        public void Submit_DescriptionStaysMandatory()
        {
            // Decision A (Allan 2026-10-28 08:39 EAT): the filled-in row is named, not reported as "no items".
            var uow = SeedRequisitionSetup();
            var form = Form(NewLine("Desk", "  ", DeskTypeId, DeskSubTypeId));
            Assert.IsTrue(form.Validate(null).Any(x => x.ErrorMessage == "Line 1: enter a description."));
            var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(form, "requester"));
            Assert.AreEqual("Line 1: enter a description.", ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());

            var nothing = Form(new PurchaseRequestLineCreateVm { Description = " ", Quantity = 1 });
            Assert.IsTrue(nothing.Validate(null).Any(x => x.ErrorMessage == "Describe at least one item to order."), "a form with only blank rows");
            Assert.AreEqual("Describe at least one item to order.", Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(nothing, "requester")).Message);
        }

        // ---------------- decision A (Allan 2026-10-28 08:39 EAT): no silently dropped lines ----------------

        [Test]
        public void DecisionA_FilledRowWithoutDescription_IsRejectedNamingTheRow_NothingSaved()
        {
            var uow = SeedRequisitionSetup();
            var form = Form(NewLine("Desk", "Office desk", DeskTypeId, DeskSubTypeId), NewLine("Laptop", "", LaptopTypeId, LaptopSubTypeId));
            var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(form, "requester"));
            Assert.AreEqual("Line 2: enter a description.", ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count(), "line 1 is not saved on its own");
            Assert.AreEqual(0, uow.Repository<PurchaseRequestLine>().GetAll().Count());

            var error = form.Validate(null).Single(x => x.ErrorMessage == "Line 2: enter a description.");
            CollectionAssert.AreEqual(new[] { "Lines[1].Description" }, error.MemberNames.ToArray(), "shown under the row's description box");
        }

        [Test]
        public void DecisionA_NameTypeSubTypeCategoryOrTagAlone_EachCountsAsAFilledRow()
        {
            var rows = new[]
            {
                new PurchaseRequestLineCreateVm { ItemName = "Chair", Quantity = 1 },
                new PurchaseRequestLineCreateVm { AssetTypeId = DeskTypeId, Quantity = 1 },
                new PurchaseRequestLineCreateVm { AssetSubTypeId = DeskSubTypeId, Quantity = 1 },
                new PurchaseRequestLineCreateVm { AssetCategoryId = 2, Quantity = 1 },
                new PurchaseRequestLineCreateVm { TargetAssetId = TaggedAssetId, Quantity = 1 }
            };
            foreach (var row in rows)
            {
                var uow = SeedRequisitionSetup();
                var form = Form(NewLine("Desk", "Office desk", DeskTypeId, DeskSubTypeId), row);
                var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(form, "requester"));
                Assert.AreEqual("Line 2: enter a description.", ex.Message);
                Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());
            }
        }

        [Test]
        public void DecisionA_SeveralRows_AreAllNamed_InFormOrder()
        {
            var uow = SeedRequisitionSetup();
            var form = Form(NewLine("Desk", null, DeskTypeId, DeskSubTypeId), NewLine("Desk 2", "Desk", DeskTypeId, DeskSubTypeId), NewLine("Laptop", "  ", LaptopTypeId, LaptopSubTypeId));
            var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(form, "requester"));
            Assert.AreEqual("Line 1: enter a description. Line 3: enter a description.", ex.Message);
            Assert.AreEqual(2, form.Validate(null).Count(x => x.ErrorMessage.EndsWith(": enter a description.", StringComparison.Ordinal)));
        }

        [Test]
        public void DecisionA_UntouchedBlankRow_IsStillIgnored()
        {
            var uow = SeedRequisitionSetup();
            var form = Form(NewLine("Desk", "Office desk", DeskTypeId, DeskSubTypeId), new PurchaseRequestLineCreateVm { Description = "", Quantity = 1 }, null);
            Assert.IsFalse(form.Validate(null).Any(x => x.ErrorMessage.EndsWith(": enter a description.", StringComparison.Ordinal) || x.ErrorMessage == "Describe at least one item to order."), "no errors for an untouched row");
            var result = RequestService(uow).SubmitBatch(form, "requester");
            Assert.AreEqual(1, LinesOf(uow, result.PrimaryId).Count);
        }

        [Test]
        public void DecisionA_LegacySingleItemPost_NameWithoutDescription_IsLineOne()
        {
            var uow = SeedRequisitionSetup();
            var service = new PurchaseRequestCreateVm { DepartmentId = 5, Justification = "Term 3", Currency = "KES", ItemName = "Desk", ItemDescription = " " };
            Assert.AreEqual("Line 1: enter a description.", Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(service, "requester")).Message);

            var viaController = new PurchaseRequestCreateVm { DepartmentId = 5, Justification = "Term 3", Currency = "KES", ItemName = "Desk", ItemDescription = "" };
            PurchaseRequestsController.EnsureLegacyLine(viaController);
            Assert.AreEqual("Line 1: enter a description.", Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(viaController, "requester")).Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRequest>().GetAll().Count());
        }

        [Test]
        public void Submit_StoresTrimmedName_AndDerivesTheCategoryFromTheType_IgnoringAPostedCategory()
        {
            var uow = SeedRequisitionSetup();
            var result = RequestService(uow).SubmitBatch(Form(NewLine("  Staff desk  ", "Desk for the office", DeskTypeId, DeskSubTypeId, categoryId: 1)), "requester");
            var line = LinesOf(uow, result.PrimaryId).Single();
            Assert.AreEqual("Staff desk", line.ItemName);
            Assert.AreEqual("Desk for the office", line.Description);
            Assert.AreEqual(2, line.AssetCategoryId, "category comes from the type (Furniture), not the posted ICT");
            Assert.AreEqual(DeskTypeId, line.AssetTypeId);
            Assert.AreEqual(DeskSubTypeId, line.AssetSubTypeId);
            Assert.IsNull(line.TargetAssetId);
        }

        [Test]
        public void Submit_TaggedLine_TakesNameAndClassificationFromTheAsset()
        {
            var uow = SeedRequisitionSetup();
            var result = RequestService(uow).SubmitBatch(Form(TaggedLine(null, "Replacement for lab laptop")), "requester");
            var pr = uow.Repository<PurchaseRequest>().GetById(result.PrimaryId);
            var line = LinesOf(uow, pr.Id).Single();
            Assert.AreEqual("Lab laptop 7", line.ItemName, "blank name on a tagged line defaults to the asset's name");
            Assert.AreEqual(TaggedAssetId, line.TargetAssetId);
            Assert.AreEqual(LaptopTypeId, line.AssetTypeId);
            Assert.AreEqual(LaptopSubTypeId, line.AssetSubTypeId);
            Assert.AreEqual(1, line.AssetCategoryId);
            Assert.AreEqual(TaggedAssetId, pr.TargetAssetId, "header tag kept for back-compat");
            Assert.AreEqual("7", pr.ApprovalStageRoleIds, "routed on the asset's type");

            var edited = RequestService(uow).SubmitBatch(Form(TaggedLine("Lab laptop 7 (2026)", "Replacement")), "requester");
            Assert.AreEqual("Lab laptop 7 (2026)", LinesOf(uow, edited.PrimaryId).Single().ItemName, "name stays editable");
            Assert.AreEqual("Lab laptop 7", uow.Repository<Asset>().GetById(TaggedAssetId).AssetName, "the tagged asset is never changed");
        }

        [Test]
        public void Submit_Split_EachRequisitionKeepsTheTagOfItsOwnLines()
        {
            var uow = SeedRequisitionSetup();
            // The tag is on line 2: before 108 only line 1 could carry it, and the split lost it.
            var result = RequestService(uow).SubmitBatch(Form(
                NewLine("Staff desk", "Desk", DeskTypeId, DeskSubTypeId),
                TaggedLine("Replacement laptop", "Laptop")), "requester");

            Assert.IsTrue(result.IsSplit);
            var requests = result.RequestIds.Select(id => uow.Repository<PurchaseRequest>().GetById(id)).ToList();
            var laptopPr = requests.Single(x => LinesOf(uow, x.Id).Any(l => l.TargetAssetId == TaggedAssetId));
            var deskPr = requests.Single(x => x.Id != laptopPr.Id);
            Assert.AreEqual(TaggedAssetId, laptopPr.TargetAssetId);
            Assert.IsNull(deskPr.TargetAssetId);
            Assert.IsNull(LinesOf(uow, deskPr.Id).Single().TargetAssetId);
            Assert.AreEqual("Replacement laptop", LinesOf(uow, laptopPr.Id).Single().ItemName);
        }

        [Test]
        public void Submit_LegacyHeaderTag_AppliesToLineOne()
        {
            var uow = SeedRequisitionSetup();
            var form = Form(new PurchaseRequestLineCreateVm { Description = "Battery", Quantity = 1 }, NewLine("Staff desk", "Desk", DeskTypeId, DeskSubTypeId));
            form.TargetAssetId = TaggedAssetId;
            var result = RequestService(uow).SubmitBatch(form, "requester");
            var all = uow.Repository<PurchaseRequestLine>().GetAll().ToList();
            Assert.AreEqual(TaggedAssetId, all.Single(x => x.Description == "Battery").TargetAssetId);
            Assert.IsNull(all.Single(x => x.Description == "Desk").TargetAssetId);
            Assert.IsTrue(result.RequestIds.Count >= 1);
        }

        [Test]
        public void Submit_TagOfAnotherOrganisation_IsRejectedPerLine()
        {
            var uow = SeedRequisitionSetup();
            uow.Seed(new Asset { Id = 900, OrganizationId = 2, AssetTag = "X-900", AssetName = "Foreign", AssetTypeId = LaptopTypeId, IsActive = true });
            var line = TaggedLine("Foreign", "x");
            line.TargetAssetId = 900;
            var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(Form(NewLine("Desk", "Desk", DeskTypeId, DeskSubTypeId), line), "requester"));
            StringAssert.StartsWith("Line 2:", ex.Message);
        }

        [Test]
        public void GetById_ShowsLineNameAndTag()
        {
            var uow = SeedRequisitionSetup();
            var service = RequestService(uow);
            var result = service.SubmitBatch(Form(TaggedLine("Replacement laptop", "Laptop")), "requester");
            var detail = service.GetById(result.PrimaryId);
            var line = detail.LineItems.Single();
            Assert.AreEqual("Replacement laptop", line.ItemName);
            Assert.AreEqual(TaggedAssetId, line.TargetAssetId);
            Assert.AreEqual("ICT-500", line.TargetAssetTag);
        }

        [Test]
        public void Submit_TypeWithoutSubTypes_UsesTheReceivingWording()
        {
            var uow = SeedRequisitionSetup();
            uow.Seed(new AssetType { Id = 30, OrganizationId = 1, AssetCategoryId = 2, Name = "Shelves", IsActive = true });
            var ex = Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(Form(NewLine("Shelf", "Shelf", 30, null)), "requester"));
            Assert.AreEqual("Line 1: this asset type has no active sub-types yet. Add one under Asset Categories first; every requisitioned asset needs a sub-type.", ex.Message);
        }

        [Test]
        public void Controller_LegacyPostWithoutLines_KeepsTheItemName()
        {
            var form = new PurchaseRequestCreateVm { DepartmentId = 5, Justification = "Term 3", Currency = "KES", ItemName = "Staff desk", ItemDescription = "Desk for the office", Quantity = 2, Lines = null };
            PurchaseRequestsController.EnsureLegacyLine(form);
            var line = form.Lines.Single();
            Assert.AreEqual("Staff desk", line.ItemName);
            Assert.AreEqual("Desk for the office", line.Description);
            Assert.AreEqual(2, line.Quantity);

            var uow = SeedRequisitionSetup();
            line.AssetTypeId = DeskTypeId;
            line.AssetSubTypeId = DeskSubTypeId;
            var result = RequestService(uow).SubmitBatch(form, "requester");
            Assert.AreEqual("Staff desk", LinesOf(uow, result.PrimaryId).Single().ItemName, "not replaced by the description");
        }

        [Test]
        public void Controller_HeaderTag_GoesToTheFirstDescribedLine_NotABlankRow()
        {
            var form = Form(new PurchaseRequestLineCreateVm { Description = "  ", Quantity = 1 }, TaggedLine("Replacement laptop", "Laptop"));
            form.Lines[1].TargetAssetId = null;
            form.TargetAssetId = TaggedAssetId;
            PurchaseRequestsController.MoveHeaderTagToLine(form);
            Assert.IsNull(form.Lines[0].TargetAssetId);
            Assert.AreEqual(TaggedAssetId, form.Lines[1].TargetAssetId);
            Assert.IsNull(form.TargetAssetId);

            var uow = SeedRequisitionSetup();
            var result = RequestService(uow).SubmitBatch(form, "requester");
            Assert.AreEqual(TaggedAssetId, LinesOf(uow, result.PrimaryId).Single().TargetAssetId, "the tag survives the blank row being dropped");

            var empty = Form(new PurchaseRequestLineCreateVm { Description = "", Quantity = 1 });
            empty.TargetAssetId = TaggedAssetId;
            PurchaseRequestsController.MoveHeaderTagToLine(empty);
            Assert.AreEqual(TaggedAssetId, empty.Lines[0].TargetAssetId, "no described line: row 0 keeps showing the tag");

            var alreadyTagged = Form(NewLine("Desk", "Desk", DeskTypeId, DeskSubTypeId), TaggedLine("x", "Laptop"));
            alreadyTagged.TargetAssetId = 999;
            PurchaseRequestsController.MoveHeaderTagToLine(alreadyTagged);
            Assert.IsNull(alreadyTagged.Lines[0].TargetAssetId, "line tags win over a header tag");
            Assert.IsNull(alreadyTagged.TargetAssetId);
        }

        // ---------------- PO carry-over and receiving ----------------

        private static FakeUnitOfWork SeedApprovedRequisition(bool legacyLines = false)
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetCategory { Id = 1, Name = "IT", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 1, Name = "Laptop", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 2, Name = "Printer", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetSubType { Id = 10, AssetTypeId = 1, Name = "Dell Latitude 5440", Brand = "Dell", Model = "Latitude 5440", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetSubType { Id = 20, AssetTypeId = 2, Name = "HP LaserJet", Brand = "HP", Model = "LaserJet", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new Department { Id = 1, Name = "Administration", Code = "ADM", DepartmentKind = DepartmentKind.Administrative, CreatedAt = DateTime.UtcNow, IsActive = true, IsRequisitionTarget = true });
            uow.Seed(new Asset
            {
                Id = TaggedAssetId, AssetTag = "IT-500", AssetName = "Old laptop", CategoryId = 1, AssetTypeId = 1, AssetSubTypeId = 10,
                Brand = "Dell", Model = "Latitude 5440", CurrentStatus = AssetStatus.Assigned, CreatedAt = DateTime.UtcNow, IsActive = true
            });
            uow.Seed(new PurchaseRequest
            {
                Id = 100, RequestNumber = "PR-100", DepartmentId = 1, ApprovalStatus = ApprovalStatus.Approved, ItemDescription = "Laptop; Printer",
                Quantity = 3, Currency = "KES", RequestedById = "requester", TargetAssetId = legacyLines ? (int?)null : TaggedAssetId,
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            if (legacyLines)
            {
                // Rows from before migration 108: no ItemName, no line classification, no line tag.
                uow.Seed(new PurchaseRequestLine { Id = 1, PurchaseRequestId = 100, LineNumber = 1, Description = "Laptop", Quantity = 2, IsActive = true, CreatedAt = DateTime.UtcNow });
                uow.Seed(new PurchaseRequestLine { Id = 2, PurchaseRequestId = 100, LineNumber = 2, Description = "Printer", Quantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow });
            }
            else
            {
                uow.Seed(new PurchaseRequestLine
                {
                    Id = 1, PurchaseRequestId = 100, LineNumber = 1, ItemName = "Replacement laptop", Description = "Laptop", Quantity = 2,
                    TargetAssetId = TaggedAssetId, AssetCategoryId = 1, AssetTypeId = 1, AssetSubTypeId = 10, IsActive = true, CreatedAt = DateTime.UtcNow
                });
                uow.Seed(new PurchaseRequestLine
                {
                    Id = 2, PurchaseRequestId = 100, LineNumber = 2, ItemName = "Office printer", Description = "Printer", Quantity = 1,
                    AssetCategoryId = 1, AssetTypeId = 2, AssetSubTypeId = 20, IsActive = true, CreatedAt = DateTime.UtcNow
                });
            }

            return uow;
        }

        private static PurchaseRecordVm Order(int? postedType2 = null)
        {
            return new PurchaseRecordVm
            {
                PurchaseRequestId = 100, PurchaseOrderNumber = "PO-1", SupplierId = 1, PurchaseDate = DateTime.UtcNow, Currency = "KES",
                Lines = new List<PurchaseOrderLineInputVm>
                {
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 1, Quantity = 2, UnitCost = 1000m, ItemName = "forged name" },
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 2, Quantity = 1, UnitCost = 500m, AssetTypeId = postedType2 }
                }
            };
        }

        private static List<PurchaseOrderLine> PoLines(FakeUnitOfWork uow, int poId)
        {
            return uow.Repository<PurchaseOrderLine>().GetAll().Where(x => x.PurchaseRecordId == poId).OrderBy(x => x.LineNumber).ToList();
        }

        private static ReceiveOrderLineVm ReceiveLine(int lineId, int qty, string name = null)
        {
            return new ReceiveOrderLineVm
            {
                PurchaseOrderLineId = lineId, QuantityReceived = qty, Condition = "New", ItemName = name,
                TagMode = ReceiveOrderLineVm.TagModeGenerate, DestinationKind = ReceiveOrderLineVm.DestinationStore
            };
        }

        [Test]
        public void PoDraft_CarriesNameAndPrefillsTheRequisitionLineClassification()
        {
            var uow = SeedApprovedRequisition();
            var draft = TestServiceFactory.CreatePurchaseService(uow).BuildFromRequisition(100);
            Assert.AreEqual("Replacement laptop", draft.Lines[0].ItemName);
            Assert.AreEqual(1, draft.Lines[0].AssetTypeId);
            Assert.AreEqual(10, draft.Lines[0].AssetSubTypeId);
            Assert.IsTrue(draft.Lines[0].ClassificationPrefilled, "tagged line: prefilled, editable");
            Assert.AreEqual(1, draft.Lines[0].PrefilledAssetTypeId);
            Assert.AreEqual(10, draft.Lines[0].PrefilledAssetSubTypeId);
            StringAssert.Contains("existing asset", draft.Lines[0].ClassificationLabel);

            Assert.AreEqual("Office printer", draft.Lines[1].ItemName);
            Assert.AreEqual(2, draft.Lines[1].AssetTypeId, "type at PO: the requisition line's type is no longer dropped");
            Assert.AreEqual(20, draft.Lines[1].AssetSubTypeId);
            Assert.IsTrue(draft.Lines[1].ClassificationPrefilled);
            Assert.IsFalse(draft.Lines[1].ClassificationEdited);
            StringAssert.Contains("from the requisition line", draft.Lines[1].ClassificationLabel);
        }

        [Test]
        public void PoCreate_CopiesNameAndClassificationPerLine_FromTheRequisition_WhenNothingIsEdited()
        {
            var uow = SeedApprovedRequisition();
            var audit = new ClassificationAuditWriter();
            var poId = TestServiceFactory.CreatePurchaseService(uow, audit).Create(Order());
            var lines = PoLines(uow, poId);
            Assert.AreEqual("Replacement laptop", lines[0].ItemName, "never taken from the post");
            Assert.AreEqual(1, lines[0].AssetTypeId);
            Assert.AreEqual(10, lines[0].AssetSubTypeId);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionAsset, lines[0].ClassificationSource);

            Assert.AreEqual("Office printer", lines[1].ItemName);
            Assert.AreEqual(2, lines[1].AssetTypeId, "the requisition line's type (prefill, not edited)");
            Assert.AreEqual(20, lines[1].AssetSubTypeId);
            Assert.AreEqual(1, lines[1].AssetCategoryId);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionLine, lines[1].ClassificationSource);
            Assert.IsFalse(audit.Entries.Any(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedOnOrder), "no edit, no edit audit");
        }

        [Test]
        public void PoCreate_PostedSameAsPrefill_IsNotAnEdit()
        {
            var uow = SeedApprovedRequisition();
            var audit = new ClassificationAuditWriter();
            var order = Order(postedType2: 2);
            order.Lines[1].AssetSubTypeId = 20;
            var lines = PoLines(uow, TestServiceFactory.CreatePurchaseService(uow, audit).Create(order));
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionLine, lines[1].ClassificationSource);
            Assert.IsFalse(audit.Entries.Any(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedOnOrder));
        }

        [Test]
        public void PoCreate_BuyerEditsTheClassification_ValidatedSavedAndAudited()
        {
            var uow = SeedApprovedRequisition();
            var audit = new ClassificationAuditWriter();
            var order = Order(postedType2: 1);
            order.Lines[1].AssetSubTypeId = 10;
            var poId = TestServiceFactory.CreatePurchaseService(uow, audit).Create(order);
            var lines = PoLines(uow, poId);
            Assert.AreEqual(1, lines[1].AssetTypeId, "the buyer's type");
            Assert.AreEqual(10, lines[1].AssetSubTypeId);
            Assert.AreEqual(1, lines[1].AssetCategoryId, "category derived from the type");
            Assert.AreEqual(PurchaseOrderLedger.SourceEditedOnOrder, lines[1].ClassificationSource);
            Assert.AreEqual("Office printer", lines[1].ItemName);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionAsset, lines[0].ClassificationSource, "the other line is unchanged");

            var edit = audit.Entries.Single(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedOnOrder);
            Assert.AreEqual(nameof(PurchaseOrderLine), edit.EntityType);
            Assert.AreEqual(lines[1].Id.ToString(), edit.EntityId);
            StringAssert.Contains("Printer (#2)", edit.OldValues);
            StringAssert.Contains("HP LaserJet (#20)", edit.OldValues);
            StringAssert.Contains("Laptop (#1)", edit.NewValues);
            StringAssert.Contains("Dell Latitude 5440 (#10)", edit.NewValues);
            StringAssert.Contains("PO-1", edit.NewValues);
            StringAssert.Contains("PR-100", edit.NewValues);
        }

        [Test]
        public void PoCreate_TaggedLine_IsEditableToo_AndAudited()
        {
            var uow = SeedApprovedRequisition();
            var audit = new ClassificationAuditWriter();
            var order = Order();
            order.Lines[0].AssetTypeId = 2;
            order.Lines[0].AssetSubTypeId = 20;
            var lines = PoLines(uow, TestServiceFactory.CreatePurchaseService(uow, audit).Create(order));
            Assert.AreEqual(2, lines[0].AssetTypeId);
            Assert.AreEqual(20, lines[0].AssetSubTypeId);
            Assert.AreEqual(PurchaseOrderLedger.SourceEditedOnOrder, lines[0].ClassificationSource);
            Assert.AreEqual(TaggedAssetId, uow.Repository<PurchaseRequestLine>().GetById(1).TargetAssetId, "the requisition line keeps its tag");
            Assert.AreEqual(1, audit.Entries.Count(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedOnOrder));
        }

        [Test]
        public void PoCreate_EditWithoutSubType_IsRejected_NothingSaved()
        {
            var uow = SeedApprovedRequisition();
            var ex = Assert.Throws<BusinessException>(() => TestServiceFactory.CreatePurchaseService(uow).Create(Order(postedType2: 1)));
            StringAssert.Contains("sub-type", ex.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseOrderLine>().GetAll().Count());
        }

        [Test]
        public void PoCreate_EditWithASubTypeOfAnotherType_IsRejected()
        {
            var uow = SeedApprovedRequisition();
            var order = Order(postedType2: 1);
            order.Lines[1].AssetSubTypeId = 20; // HP LaserJet belongs to Printer, not Laptop
            var ex = Assert.Throws<BusinessException>(() => TestServiceFactory.CreatePurchaseService(uow).Create(order));
            StringAssert.Contains("does not belong", ex.Message);
        }

        [Test]
        public void PoCreate_EditToAnInactiveTypeOrSubType_IsRejected()
        {
            var uow = SeedApprovedRequisition();
            uow.Seed(new AssetType { Id = 3, Name = "Old scanners", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = false });
            uow.Seed(new AssetSubType { Id = 30, AssetTypeId = 3, Name = "Scanner", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetSubType { Id = 11, AssetTypeId = 1, Name = "Retired model", IsActive = false, CreatedAt = DateTime.UtcNow });

            var inactiveType = Order(postedType2: 3);
            inactiveType.Lines[1].AssetSubTypeId = 30;
            StringAssert.Contains("not found", Assert.Throws<BusinessException>(() => TestServiceFactory.CreatePurchaseService(uow).Create(inactiveType)).Message);

            var inactiveSub = Order(postedType2: 1);
            inactiveSub.Lines[1].AssetSubTypeId = 11;
            StringAssert.Contains("inactive", Assert.Throws<BusinessException>(() => TestServiceFactory.CreatePurchaseService(uow).Create(inactiveSub)).Message);
            Assert.AreEqual(0, uow.Repository<PurchaseOrderLine>().GetAll().Count());
        }

        [Test]
        public void Ledger_EditedClassification_OtherOrganisation_IsRejected_SharedIsAllowed()
        {
            var uow = SeedApprovedRequisition();
            uow.Seed(new AssetType { Id = 4, OrganizationId = 2, Name = "Other org type", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetSubType { Id = 40, OrganizationId = 2, AssetTypeId = 4, Name = "Other org sub", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetSubType { Id = 12, OrganizationId = 2, AssetTypeId = 1, Name = "Other org laptop", IsActive = true, CreatedAt = DateTime.UtcNow });
            var ledger = new PurchaseOrderLedger(uow);

            StringAssert.Contains("not found", Assert.Throws<BusinessException>(() => ledger.ValidateEditedClassification(1, 4, 40, "line 1", "x")).Message);
            StringAssert.Contains("not found", Assert.Throws<BusinessException>(() => ledger.ValidateEditedClassification(1, 1, 12, "line 1", "x")).Message);
            var ok = ledger.ValidateEditedClassification(1, 1, 10, "line 1", "x");
            Assert.AreEqual(1, ok.AssetCategoryId, "shared (OrganizationId NULL) rows are allowed; category from the type");
            Assert.AreEqual("Dell", ok.Brand);
        }

        [Test]
        public void Ledger_EditedClassification_TypeWithoutSubTypes_SaysAddOneFirst()
        {
            var uow = SeedApprovedRequisition();
            uow.Seed(new AssetType { Id = 5, Name = "Projectors", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            var ex = Assert.Throws<BusinessException>(() => new PurchaseOrderLedger(uow).ValidateEditedClassification(null, 5, null, "line 2", "x"));
            StringAssert.Contains("no active sub-types", ex.Message);
        }

        [Test]
        public void Po_UsesTheApprovedLineClassification_WhenTheTaggedAssetWasReclassifiedAfterApproval()
        {
            var uow = SeedApprovedRequisition();
            var asset = uow.Repository<Asset>().GetById(TaggedAssetId);
            asset.AssetTypeId = 2;
            asset.AssetSubTypeId = 20;

            var purchases = TestServiceFactory.CreatePurchaseService(uow);
            var draft = purchases.BuildFromRequisition(100);
            Assert.AreEqual(1, draft.Lines[0].AssetTypeId, "stored (approved) line type, not the asset's current one");
            Assert.AreEqual(10, draft.Lines[0].AssetSubTypeId);

            var poLine = PoLines(uow, purchases.Create(Order()))[0];
            Assert.AreEqual(1, poLine.AssetTypeId);
            Assert.AreEqual(10, poLine.AssetSubTypeId);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionAsset, poLine.ClassificationSource);
        }

        [Test]
        public void Ledger_TaggedLineWithoutStoredType_UsesTheAssetsCurrentClassification()
        {
            var uow = SeedApprovedRequisition();
            var line = new PurchaseRequestLine { Id = 3, PurchaseRequestId = 100, LineNumber = 3, Description = "Laptop", Quantity = 1, TargetAssetId = TaggedAssetId };
            var classification = new PurchaseOrderLedger(uow).FromRequisitionLine(line);
            Assert.AreEqual(1, classification.AssetTypeId);
            Assert.AreEqual(10, classification.AssetSubTypeId);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionAsset, classification.Source);
        }

        [Test]
        public void Ledger_PoLineWithoutType_FallsBackToItsRequisitionLine()
        {
            var uow = SeedApprovedRequisition();
            var ledger = new PurchaseOrderLedger(uow);
            var request = uow.Repository<PurchaseRequest>().GetById(100);
            var classification = ledger.ResolveClassification(
                new PurchaseRecord { Id = 1, SupplierId = 1 },
                request,
                new PurchaseOrderLine { Id = 7, PurchaseRequestLineId = 2, Description = "Printer" },
                "Printer");
            Assert.AreEqual(2, classification.AssetTypeId);
            Assert.AreEqual(20, classification.AssetSubTypeId);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionLine, classification.Source);
            Assert.AreEqual("Office printer", ledger.ResolveItemName(request, new PurchaseOrderLine { PurchaseRequestLineId = 2 }));
        }

        [Test]
        public void Receiving_PrefillsTheName_UsesThePostedName_AndTaggedLineCreatesANewAsset()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var receiving = TestServiceFactory.CreateReceivingService(uow);

            var screen = receiving.GetReceiveOrder(poId);
            Assert.AreEqual("Replacement laptop", screen.Lines[0].ItemName);
            Assert.AreEqual("Office printer", screen.Lines[1].ItemName);

            var assetsBefore = uow.Repository<Asset>().GetAll().Count();
            var result = receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[0].Id, 2, "  Lab laptop 8  "), ReceiveLine(lines[1].Id, 1, "") }
            }, "receiver");

            Assert.AreEqual(3, result.CreatedAssets.Count);
            Assert.AreEqual(assetsBefore + 3, uow.Repository<Asset>().GetAll().Count(), "receiving against a tag always creates new assets");
            var created = result.CreatedAssets.Select(x => uow.Repository<Asset>().GetById(x.AssetId)).ToList();
            Assert.AreEqual(2, created.Count(x => x.AssetName == "Lab laptop 8"), "posted name, trimmed");
            Assert.IsTrue(created.Where(x => x.AssetName == "Lab laptop 8").All(x => x.AssetTypeId == 1 && x.AssetSubTypeId == 10 && x.Id != TaggedAssetId), "tag's classification");
            Assert.AreEqual(1, created.Count(x => x.AssetName == "Office printer" && x.AssetTypeId == 2 && x.AssetSubTypeId == 20), "blank post = PO line ItemName");

            var tagged = uow.Repository<Asset>().GetById(TaggedAssetId);
            Assert.AreEqual("Old laptop", tagged.AssetName, "the tagged asset is not updated");
            Assert.AreEqual(AssetStatus.Assigned, tagged.CurrentStatus);
        }

        [Test]
        public void Receiving_PostedNameTooLong_IsRejected_NothingCreated()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var before = uow.Repository<Asset>().GetAll().Count();
            var ex = Assert.Throws<BusinessException>(() => TestServiceFactory.CreateReceivingService(uow).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[1].Id, 1, new string('x', AssetNameRules.MaxLength + 1)) }
            }, "receiver"));
            StringAssert.Contains("cannot be longer than " + AssetNameRules.MaxLength, ex.Message);
            StringAssert.Contains("line", ex.Message);
            Assert.AreEqual(before, uow.Repository<Asset>().GetAll().Count());
        }

        private static ReceiveOrderLineVm EditedReceiveLine(int lineId, int qty, int? typeId, int? subTypeId)
        {
            var line = ReceiveLine(lineId, qty);
            line.OverrideAssetTypeId = typeId;
            line.OverrideAssetSubTypeId = subTypeId;
            return line;
        }

        [Test]
        public void Receiving_EditedClassification_GoesOnTheNewAsset_PoLineKeepsIts_Audited()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var audit = new ClassificationAuditWriter();
            var result = TestServiceFactory.CreateReceivingService(uow, auditWriter: audit).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, 10) }
            }, "receiver");

            var asset = uow.Repository<Asset>().GetById(result.CreatedAssets.Single().AssetId);
            Assert.AreEqual(1, asset.AssetTypeId, "the receiver's type");
            Assert.AreEqual(10, asset.AssetSubTypeId);
            Assert.AreEqual(1, asset.CategoryId, "category derived from the type");
            Assert.AreEqual("Dell", asset.Brand, "brand / model follow the edited sub-type");
            Assert.AreEqual("Office printer", asset.AssetName);
            var poLine = uow.Repository<PurchaseOrderLine>().GetById(lines[1].Id);
            Assert.AreEqual(2, poLine.AssetTypeId, "the PO line keeps what was ordered");
            Assert.AreEqual(20, poLine.AssetSubTypeId);

            var edit = audit.Entries.Single(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedAtReceiving);
            Assert.AreEqual(nameof(PurchaseOrderLine), edit.EntityType);
            Assert.AreEqual(lines[1].Id.ToString(), edit.EntityId);
            StringAssert.Contains("Printer (#2)", edit.OldValues);
            StringAssert.Contains("Laptop (#1)", edit.NewValues);
            StringAssert.Contains("receipt " + result.ReceivingId, edit.NewValues);
            StringAssert.Contains("assets " + asset.Id, edit.NewValues);
            StringAssert.Contains("classification changed at receiving", audit.Entries.Single(x => x.Action == "Purchases.Receive").NewValues);
        }

        [Test]
        public void Receiving_TaggedLineEdited_StillCreatesANewAsset_TaggedAssetUntouched()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var audit = new ClassificationAuditWriter();
            var result = TestServiceFactory.CreateReceivingService(uow, auditWriter: audit).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[0].Id, 1, 2, 20) }
            }, "receiver");

            var created = uow.Repository<Asset>().GetById(result.CreatedAssets.Single().AssetId);
            Assert.AreNotEqual(TaggedAssetId, created.Id);
            Assert.AreEqual(2, created.AssetTypeId);
            Assert.AreEqual(20, created.AssetSubTypeId);
            var tagged = uow.Repository<Asset>().GetById(TaggedAssetId);
            Assert.AreEqual(1, tagged.AssetTypeId, "the tagged asset is not reclassified");
            Assert.AreEqual("Old laptop", tagged.AssetName);
            Assert.AreEqual(1, audit.Entries.Count(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedAtReceiving));
        }

        [Test]
        public void Receiving_EditWithoutSubTypeOrWithAWrongSubType_IsRejected_NothingCreated()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var before = uow.Repository<Asset>().GetAll().Count();
            var receiving = TestServiceFactory.CreateReceivingService(uow);

            var noSub = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, null) }
            }, "receiver"));
            StringAssert.Contains("sub-type", noSub.Message);

            var wrongSub = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, 20) }
            }, "receiver"));
            StringAssert.Contains("does not belong", wrongSub.Message);
            Assert.AreEqual(before, uow.Repository<Asset>().GetAll().Count());
        }

        [Test]
        public void Receiving_PostedSameAsThePoLine_IsNotAnEdit()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var audit = new ClassificationAuditWriter();
            var result = TestServiceFactory.CreateReceivingService(uow, auditWriter: audit).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 2, 20) }
            }, "receiver");
            Assert.AreEqual(20, uow.Repository<Asset>().GetById(result.CreatedAssets.Single().AssetId).AssetSubTypeId);
            Assert.IsFalse(audit.Entries.Any(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedAtReceiving));
        }

        // ---------------- decision B (Allan 2026-10-28 08:39 EAT): Edit panel blanked at receiving ----------------

        private static ReceiveOrderLineVm OpenedEditLine(int lineId, int? typeId, int? subTypeId)
        {
            var line = EditedReceiveLine(lineId, 1, typeId, subTypeId);
            line.ClassificationEditOpened = true;
            return line;
        }

        [Test]
        public void DecisionB_EditOpened_TypeOrSubTypeBlanked_IsRejected_NoFallbackToThePoLine_NothingCreated()
        {
            var cases = new[]
            {
                new int?[] { null, null },
                new int?[] { null, 20 },
                new int?[] { 2, null },
                new int?[] { 0, 0 }
            };
            foreach (var c in cases)
            {
                var uow = SeedApprovedRequisition();
                var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
                var lines = PoLines(uow, poId);
                var assetsBefore = uow.Repository<Asset>().GetAll().Count();
                var ex = Assert.Throws<BusinessException>(() => TestServiceFactory.CreateReceivingService(uow).ReceiveOrder(new ReceiveOrderVm
                {
                    PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { OpenedEditLine(lines[1].Id, c[0], c[1]) }
                }, "receiver"), "type " + c[0] + " / sub-type " + c[1]);
                StringAssert.StartsWith("Choose the asset type and sub-type (line ", ex.Message);
                StringAssert.Contains("category, type and sub-type", ex.Message);
                Assert.AreEqual(assetsBefore, uow.Repository<Asset>().GetAll().Count());
                Assert.AreEqual(0, uow.Repository<AssetReceiving>().GetAll().Count());
                Assert.AreEqual(0, uow.Repository<PurchaseOrderLineReceipt>().GetAll().Count());
            }
        }

        [Test]
        public void DecisionB_PanelNotOpened_ThePoLineClassificationStands()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var result = TestServiceFactory.CreateReceivingService(uow).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[1].Id, 1) }
            }, "receiver");
            var asset = uow.Repository<Asset>().GetById(result.CreatedAssets.Single().AssetId);
            Assert.AreEqual(2, asset.AssetTypeId, "the PO line's type");
            Assert.AreEqual(20, asset.AssetSubTypeId);
            Assert.Greater(asset.CategoryId, 0, "category from the PO line's type");
        }

        [Test]
        public void DecisionB_EditOpened_WithAFullClassification_IsAccepted()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var audit = new ClassificationAuditWriter();
            var unchanged = TestServiceFactory.CreateReceivingService(uow, auditWriter: audit).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { OpenedEditLine(lines[0].Id, 1, 10) }
            }, "receiver");
            Assert.AreEqual(10, uow.Repository<Asset>().GetById(unchanged.CreatedAssets.Single().AssetId).AssetSubTypeId);
            Assert.IsFalse(audit.Entries.Any(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedAtReceiving), "opened but unchanged is not an edit");

            var edited = TestServiceFactory.CreateReceivingService(uow, auditWriter: audit).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { OpenedEditLine(lines[1].Id, 1, 10) }
            }, "receiver");
            Assert.AreEqual(1, uow.Repository<Asset>().GetById(edited.CreatedAssets.Single().AssetId).AssetTypeId);
            Assert.AreEqual(1, audit.Entries.Count(x => x.Action == PurchaseOrderLedger.AuditActionClassificationEditedAtReceiving));
        }

        // ---------------- C3 override trail (migration 108 columns) and C5 receive block ----------------

        [Test]
        public void PoCreate_Edit_RecordsWhoWhenAndTheOriginal_OnTheLine_AndTheOrderStatusShowsIt()
        {
            var uow = SeedApprovedRequisition();
            var order = Order(postedType2: 1);
            order.Lines[1].AssetSubTypeId = 10;
            var before = DateTime.UtcNow.AddSeconds(-1);
            var purchases = TestServiceFactory.CreatePurchaseService(uow);
            var poId = purchases.Create(order, "buyer-1", null);
            var lines = PoLines(uow, poId);

            Assert.AreEqual("buyer-1", lines[1].ClassificationOverriddenById);
            Assert.IsTrue(lines[1].ClassificationOverriddenAt.HasValue && lines[1].ClassificationOverriddenAt.Value >= before, "UTC time of the change");
            Assert.AreEqual(1, lines[1].OriginalAssetCategoryId);
            Assert.AreEqual(2, lines[1].OriginalAssetTypeId, "the prefill it replaced");
            Assert.AreEqual(20, lines[1].OriginalAssetSubTypeId);
            Assert.IsNull(lines[0].ClassificationOverriddenById, "not edited: no trail");
            Assert.IsNull(lines[0].ClassificationOverriddenAt);
            Assert.IsNull(lines[0].OriginalAssetTypeId);

            var status = purchases.GetOrderStatus(poId);
            var change = status.Lines.Single(x => x.Id == lines[1].Id).ClassificationOverride;
            Assert.IsNotNull(change);
            Assert.AreEqual("IT > Printer > HP LaserJet", change.OldClassification);
            Assert.AreEqual("IT > Laptop > Dell Latitude 5440", change.NewClassification);
            Assert.AreEqual("buyer-1", change.ChangedByDisplay, "no user directory in the test: falls back to the id");
            Assert.AreEqual(lines[1].ClassificationOverriddenAt, change.ChangedAtUtc);
            Assert.IsNull(status.Lines.Single(x => x.Id == lines[0].Id).ClassificationOverride);
        }

        [Test]
        public void Receiving_Edit_RecordsWhoWhenAndTheOriginal_OnTheReceipt_AndTheReceivingListShowsIt()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var receiving = TestServiceFactory.CreateReceivingService(uow);
            var result = receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId,
                ReceivedDate = DateTime.UtcNow,
                Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, 10), ReceiveLine(lines[0].Id, 1) }
            }, "receiver-7");

            var receipts = uow.Repository<PurchaseOrderLineReceipt>().GetAll().Where(x => x.PurchaseRecordId == poId).ToList();
            var edited = receipts.Single(x => x.PurchaseOrderLineId == lines[1].Id);
            Assert.AreEqual("receiver-7", edited.ClassificationOverriddenById);
            Assert.IsTrue(edited.ClassificationOverriddenAt.HasValue);
            Assert.AreEqual(1, edited.OriginalAssetCategoryId);
            Assert.AreEqual(2, edited.OriginalAssetTypeId, "the PO line's classification it replaced");
            Assert.AreEqual(20, edited.OriginalAssetSubTypeId);
            var plain = receipts.Single(x => x.PurchaseOrderLineId == lines[0].Id);
            Assert.IsNull(plain.ClassificationOverriddenById);
            Assert.IsNull(plain.ClassificationOverriddenAt);
            Assert.IsNull(plain.OriginalAssetTypeId);
            Assert.IsNull(uow.Repository<PurchaseOrderLine>().GetById(lines[1].Id).ClassificationOverriddenAt, "the PO line was not edited on the order");

            var list = receiving.GetReceivingsForPurchase(poId).ToList();
            var shown = list.Single(x => x.Id == edited.AssetReceivingId).ClassificationOverride;
            Assert.IsNotNull(shown);
            Assert.AreEqual("IT > Printer > HP LaserJet", shown.OldClassification);
            Assert.AreEqual("IT > Laptop > Dell Latitude 5440", shown.NewClassification, "the new asset's classification");
            Assert.AreEqual("receiver-7", shown.ChangedByDisplay);
            Assert.IsNull(list.Single(x => x.Id == plain.AssetReceivingId).ClassificationOverride);
            Assert.AreEqual(2, result.CreatedAssets.Count);
        }

        [Test]
        public void ClassificationOverride_ChangedByDisplay_PrefersTheName()
        {
            Assert.AreEqual("Jane Buyer", new ClassificationOverrideVm { ChangedById = "u1", ChangedByName = "Jane Buyer" }.ChangedByDisplay);
            Assert.AreEqual("u1", new ClassificationOverrideVm { ChangedById = "u1" }.ChangedByDisplay);
            Assert.AreEqual("unknown user", new ClassificationOverrideVm().ChangedByDisplay);
        }

        [Test]
        public void C5_ReceivingBlocks_UnlessCategoryTypeAndSubTypeAreAllSet()
        {
            StringAssert.Contains("asset type", Assert.Throws<BusinessException>(() => ReceivingService.EnsureCompleteClassification(new PurchaseLineClassification(), "line 1")).Message);
            StringAssert.Contains("category", Assert.Throws<BusinessException>(() => ReceivingService.EnsureCompleteClassification(new PurchaseLineClassification { AssetTypeId = 1, AssetSubTypeId = 10 }, "line 1")).Message);
            StringAssert.Contains("sub-type", Assert.Throws<BusinessException>(() => ReceivingService.EnsureCompleteClassification(new PurchaseLineClassification { AssetCategoryId = 1, AssetTypeId = 1 }, "line 1")).Message);
            Assert.DoesNotThrow(() => ReceivingService.EnsureCompleteClassification(new PurchaseLineClassification { AssetCategoryId = 1, AssetTypeId = 1, AssetSubTypeId = 10 }, "line 1"));
        }

        [Test]
        public void C5_ReceivingEditedToATypeWithoutSubTypes_IsBlocked_NothingCreated()
        {
            var uow = SeedApprovedRequisition();
            uow.Seed(new AssetType { Id = 5, Name = "Projectors", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var before = uow.Repository<Asset>().GetAll().Count();
            var ex = Assert.Throws<BusinessException>(() => TestServiceFactory.CreateReceivingService(uow).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 5, null) }
            }, "receiver"));
            StringAssert.Contains("no active sub-types", ex.Message);
            Assert.AreEqual(before, uow.Repository<Asset>().GetAll().Count());
            Assert.AreEqual(0, uow.Repository<PurchaseOrderLineReceipt>().GetAll().Count());
        }

        // ---------------- Re-review fixes: organisation checks, persisted "new" on receipts ----------------

        private static void SeedOtherOrganisationClassification(FakeUnitOfWork uow)
        {
            uow.Seed(new AssetType { Id = 4, OrganizationId = 2, Name = "Other org type", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetSubType { Id = 40, OrganizationId = 2, AssetTypeId = 4, Name = "Other org sub", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetSubType { Id = 12, OrganizationId = 2, AssetTypeId = 1, Name = "Other org laptop", IsActive = true, CreatedAt = DateTime.UtcNow });
        }

        private static void ClearPurchaseOrganisation(FakeUnitOfWork uow, int poId)
        {
            var po = uow.Repository<PurchaseRecord>().GetById(poId);
            po.OrganizationId = null;
            uow.Repository<PurchaseRecord>().Update(po);
        }

        [Test]
        public void Receiving_EditOnAPoWithoutOrganisation_UsesTheCurrentTenant_OtherOrganisationIsRejected()
        {
            var uow = SeedApprovedRequisition();
            SeedOtherOrganisationClassification(uow);
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            ClearPurchaseOrganisation(uow, poId);
            var lines = PoLines(uow, poId);
            var receiving = TestServiceFactory.CreateReceivingService(uow, new FakeOrganizationScopeService(organizationId: 1));
            var before = uow.Repository<Asset>().GetAll().Count();

            var foreignType = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 4, 40) }
            }, "receiver"));
            StringAssert.Contains("asset type was not found", foreignType.Message);

            var foreignSub = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, 12) }
            }, "receiver"));
            StringAssert.Contains("sub-type was not found", foreignSub.Message);
            Assert.AreEqual(before, uow.Repository<Asset>().GetAll().Count());

            receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, 10) }
            }, "receiver");
            Assert.AreEqual(before + 1, uow.Repository<Asset>().GetAll().Count(), "shared (OrganizationId NULL) rows are still allowed");
        }

        [Test]
        public void Receiving_UnclassifiedLine_TypeOrSubTypeOfAnotherOrganisation_IsRejected()
        {
            var uow = SeedApprovedRequisition(legacyLines: true);
            SeedOtherOrganisationClassification(uow);
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            Assert.IsNull(lines[1].AssetTypeId, "legacy line: nothing to prefill, the receiver picks the type");
            var receiving = TestServiceFactory.CreateReceivingService(uow);

            var foreignType = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 4, 40) }
            }, "receiver"));
            StringAssert.Contains("asset type was not found", foreignType.Message);

            var foreignSub = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, 12) }
            }, "receiver"));
            StringAssert.Contains("sub-type", foreignSub.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseOrderLineReceipt>().GetAll().Count());
        }

        [Test]
        public void PoCreate_UnprefilledLine_TypeOrSubTypeOfAnotherOrganisation_IsRejected()
        {
            var uow = SeedApprovedRequisition(legacyLines: true);
            SeedOtherOrganisationClassification(uow);
            var purchases = TestServiceFactory.CreatePurchaseService(uow);

            var foreignType = Assert.Throws<BusinessException>(() => purchases.Create(Order(postedType2: 4)));
            StringAssert.Contains("asset type was not found", foreignType.Message);

            var order = Order(postedType2: 1);
            order.Lines[1].AssetSubTypeId = 12;
            var foreignSub = Assert.Throws<BusinessException>(() => purchases.Create(order));
            StringAssert.Contains("sub-type was not found", foreignSub.Message);
            Assert.AreEqual(0, uow.Repository<PurchaseRecord>().GetAll().Count());
        }

        [Test]
        public void Receiving_Edit_PersistsTheNewClassification_TrailDoesNotFollowALaterReclassification()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            var receiving = TestServiceFactory.CreateReceivingService(uow);
            var result = receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { EditedReceiveLine(lines[1].Id, 1, 1, 10) }
            }, "receiver");

            var receipt = uow.Repository<PurchaseOrderLineReceipt>().GetAll().Single(x => x.PurchaseRecordId == poId);
            Assert.AreEqual(1, receipt.NewAssetCategoryId);
            Assert.AreEqual(1, receipt.NewAssetTypeId);
            Assert.AreEqual(10, receipt.NewAssetSubTypeId);

            // The asset is reclassified later: the trail still shows what was set at receiving.
            var asset = uow.Repository<Asset>().GetById(result.CreatedAssets.Single().AssetId);
            asset.AssetTypeId = 2;
            asset.AssetSubTypeId = 20;
            uow.Repository<Asset>().Update(asset);
            var shown = receiving.GetReceivingsForPurchase(poId).Single().ClassificationOverride;
            Assert.AreEqual("IT > Laptop > Dell Latitude 5440", shown.NewClassification);
            Assert.IsFalse(shown.NewIsCurrentFallback);

            // A receipt stamped before the New* columns: falls back to the asset's CURRENT classification, labelled.
            receipt.NewAssetCategoryId = null;
            receipt.NewAssetTypeId = null;
            receipt.NewAssetSubTypeId = null;
            uow.Repository<PurchaseOrderLineReceipt>().Update(receipt);
            var legacy = receiving.GetReceivingsForPurchase(poId).Single().ClassificationOverride;
            Assert.AreEqual("IT > Printer > HP LaserJet", legacy.NewClassification);
            Assert.IsTrue(legacy.NewIsCurrentFallback);
        }

        [Test]
        public void Receiving_NotEdited_LeavesTheNewColumnsEmpty()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order());
            var lines = PoLines(uow, poId);
            TestServiceFactory.CreateReceivingService(uow).ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[1].Id, 1) }
            }, "receiver");
            var receipt = uow.Repository<PurchaseOrderLineReceipt>().GetAll().Single(x => x.PurchaseRecordId == poId);
            Assert.IsNull(receipt.NewAssetTypeId);
            Assert.IsNull(receipt.NewAssetSubTypeId);
            Assert.IsNull(receipt.NewAssetCategoryId);
        }

        private sealed class ClassificationAuditWriter : IAuditWriter
        {
            public List<Entry> Entries { get; } = new List<Entry>();

            public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
            {
                Entries.Add(new Entry { Action = action, EntityType = entityType, EntityId = entityId, OldValues = oldValues, NewValues = newValues });
            }

            public sealed class Entry
            {
                public string Action { get; set; }
                public string EntityType { get; set; }
                public string EntityId { get; set; }
                public string OldValues { get; set; }
                public string NewValues { get; set; }
            }
        }

        [Test]
        public void LegacyRows_NullItemName_AreNotRevalidated_AndUseTheFallbackChain()
        {
            var uow = SeedApprovedRequisition(legacyLines: true);
            var purchases = TestServiceFactory.CreatePurchaseService(uow);
            var draft = purchases.BuildFromRequisition(100);
            Assert.IsNull(draft.Lines[0].ItemName);
            Assert.IsFalse(draft.Lines[0].ClassificationPrefilled, "no line classification: PO may still choose one");

            var order = new PurchaseRecordVm
            {
                PurchaseRequestId = 100, PurchaseOrderNumber = "PO-L", SupplierId = 1, PurchaseDate = DateTime.UtcNow, Currency = "KES",
                Lines = new List<PurchaseOrderLineInputVm>
                {
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 1, Quantity = 2, UnitCost = 1000m, AssetTypeId = 1, AssetSubTypeId = 10 },
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 2, Quantity = 1, UnitCost = 500m, AssetTypeId = 2, AssetSubTypeId = 20 }
                }
            };
            var poId = purchases.Create(order);
            var lines = PoLines(uow, poId);
            Assert.IsNull(lines[0].ItemName);
            Assert.AreEqual(PurchaseOrderLedger.SourceSelectedOnOrder, lines[0].ClassificationSource);

            var receiving = TestServiceFactory.CreateReceivingService(uow);
            Assert.AreEqual("Laptop", receiving.GetReceiveOrder(poId).Lines[0].ItemName, "NULL ItemName: description");
            var result = receiving.ReceiveOrder(new ReceiveOrderVm
            {
                PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = new List<ReceiveOrderLineVm> { ReceiveLine(lines[0].Id, 1) }
            }, "receiver");
            Assert.AreEqual("Laptop", uow.Repository<Asset>().GetById(result.CreatedAssets[0].AssetId).AssetName);
        }

        [Test]
        public void DefaultAssetName_Chain()
        {
            Assert.AreEqual("Item", ReceivingService.DefaultAssetName(" Item ", "Desc", "Sub", "B", "M"));
            Assert.AreEqual("Desc", ReceivingService.DefaultAssetName(null, " Desc ", "Sub", "B", "M"));
            Assert.AreEqual("Sub", ReceivingService.DefaultAssetName("", "", "Sub", "B", "M"));
            Assert.AreEqual("B M", ReceivingService.DefaultAssetName(null, null, null, "B", "M"));
            Assert.AreEqual("Received asset", ReceivingService.DefaultAssetName(null, null, null, null, null));
            Assert.AreEqual(AssetNameRules.MaxLength, ReceivingService.DefaultAssetName(null, new string('d', 400), null, null, null).Length, "long descriptions are cut, never an error");
        }

        // ---------------- AssetService guard ----------------

        private static FakeUnitOfWork SeedAssetCatalog()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department { Id = 1, Name = "IT", Code = "IT", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetCategory { Id = 1, Name = "IT", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 1, Name = "Laptop", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetSubType { Id = 7, AssetTypeId = 1, Name = "Latitude", IsActive = true, CreatedAt = DateTime.UtcNow });
            return uow;
        }

        private static AssetCreateVm NewAsset(string name)
        {
            return new AssetCreateVm
            {
                AssetName = name, CategoryId = 1, AssetTypeId = 1, AssetSubTypeId = 7, PurchaseDate = DateTime.UtcNow, AcquisitionCost = 10,
                Currency = "USD", SupplierId = 1, DepartmentId = 1
            };
        }

        [Test]
        public void AssetService_Create_TrimsAndGuardsTheName()
        {
            var uow = SeedAssetCatalog();
            var service = TestServiceFactory.CreateAssetService(uow);
            Assert.AreEqual("Enter the asset name.", Assert.Throws<BusinessException>(() => service.Create(NewAsset("   "))).Message);
            StringAssert.StartsWith("Asset name cannot be longer than " + AssetNameRules.MaxLength,
                Assert.Throws<BusinessException>(() => service.Create(NewAsset(new string('n', AssetNameRules.MaxLength + 1)))).Message);
            var id = service.Create(NewAsset("  Staff laptop  "));
            Assert.AreEqual("Staff laptop", uow.Repository<Asset>().GetById(id).AssetName);
        }

        [Test]
        public void AssetService_Update_GuardsANewName_ButKeepsAnUnchangedLegacyName()
        {
            var uow = SeedAssetCatalog();
            var service = TestServiceFactory.CreateAssetService(uow);
            var id = service.Create(NewAsset("Staff laptop"));
            var legacyName = new string('L', AssetNameRules.MaxLength); // the NVARCHAR(200) column maximum
            uow.Repository<Asset>().GetById(id).AssetName = legacyName;

            var edit = Edit(uow, id, legacyName);
            service.Update(edit);
            Assert.AreEqual(legacyName, uow.Repository<Asset>().GetById(id).AssetName, "unchanged legacy name is not re-validated");

            var ex = Assert.Throws<BusinessException>(() => service.Update(Edit(uow, id, legacyName + "x")));
            StringAssert.StartsWith("Asset name cannot be longer than", ex.Message);
            Assert.AreEqual("Enter the asset name.", Assert.Throws<BusinessException>(() => service.Update(Edit(uow, id, " "))).Message);
        }

        private static AssetEditVm Edit(FakeUnitOfWork uow, int id, string name)
        {
            var entity = uow.Repository<Asset>().GetById(id);
            return new AssetEditVm
            {
                Id = id, AssetName = name, AssetTag = entity.AssetTag, CategoryId = entity.CategoryId, AssetTypeId = entity.AssetTypeId,
                AssetSubTypeId = entity.AssetSubTypeId, PurchaseDate = entity.PurchaseDate, AcquisitionCost = entity.AcquisitionCost,
                Currency = entity.Currency, SupplierId = entity.SupplierId, DepartmentId = entity.DepartmentId, CurrentStatus = entity.CurrentStatus
            };
        }
#pragma warning restore 618
    }
}
