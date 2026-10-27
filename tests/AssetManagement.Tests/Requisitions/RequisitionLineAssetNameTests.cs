using System;
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
    /// <summary>
    /// Migration 108: requisition line "Item / asset name" + per-line tag, carried requisition -> PO -> receiving,
    /// and the AssetService name guard. Rows from before 108 (NULL ItemName, no line classification) are never
    /// re-validated and keep the old name fallback chain.
    /// </summary>
    [TestFixture]
    public class RequisitionLineAssetNameTests
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
            var uow = SeedRequisitionSetup();
            var form = Form(NewLine("Desk", "  ", DeskTypeId, DeskSubTypeId));
            Assert.IsTrue(form.Validate(null).Any(x => x.ErrorMessage == "Describe at least one item to order."));
            Assert.Throws<BusinessException>(() => RequestService(uow).SubmitBatch(form, "requester"));
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
        public void PoDraft_CarriesNameAndLocksTheRequisitionLineClassification()
        {
            var uow = SeedApprovedRequisition();
            var draft = TestServiceFactory.CreatePurchaseService(uow).BuildFromRequisition(100);
            Assert.AreEqual("Replacement laptop", draft.Lines[0].ItemName);
            Assert.AreEqual(1, draft.Lines[0].AssetTypeId);
            Assert.AreEqual(10, draft.Lines[0].AssetSubTypeId);
            Assert.IsTrue(draft.Lines[0].ClassificationLocked);
            StringAssert.Contains("existing asset", draft.Lines[0].ClassificationLabel);

            Assert.AreEqual("Office printer", draft.Lines[1].ItemName);
            Assert.AreEqual(2, draft.Lines[1].AssetTypeId, "type at PO: the requisition line's type is no longer dropped");
            Assert.AreEqual(20, draft.Lines[1].AssetSubTypeId);
            Assert.IsTrue(draft.Lines[1].ClassificationLocked);
            StringAssert.Contains("from the requisition line", draft.Lines[1].ClassificationLabel);
        }

        [Test]
        public void PoCreate_CopiesNameAndClassificationPerLine_FromTheRequisitionNotThePost()
        {
            var uow = SeedApprovedRequisition();
            var poId = TestServiceFactory.CreatePurchaseService(uow).Create(Order(postedType2: 1));
            var lines = PoLines(uow, poId);
            Assert.AreEqual("Replacement laptop", lines[0].ItemName, "never taken from the post");
            Assert.AreEqual(1, lines[0].AssetTypeId);
            Assert.AreEqual(10, lines[0].AssetSubTypeId);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionAsset, lines[0].ClassificationSource);

            Assert.AreEqual("Office printer", lines[1].ItemName);
            Assert.AreEqual(2, lines[1].AssetTypeId, "requisition line type wins over a type posted on the PO");
            Assert.AreEqual(20, lines[1].AssetSubTypeId);
            Assert.AreEqual(1, lines[1].AssetCategoryId);
            Assert.AreEqual(PurchaseOrderLedger.SourceRequisitionLine, lines[1].ClassificationSource);
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

        [Test]
        public void LegacyRows_NullItemName_AreNotRevalidated_AndUseTheFallbackChain()
        {
            var uow = SeedApprovedRequisition(legacyLines: true);
            var purchases = TestServiceFactory.CreatePurchaseService(uow);
            var draft = purchases.BuildFromRequisition(100);
            Assert.IsNull(draft.Lines[0].ItemName);
            Assert.IsFalse(draft.Lines[0].ClassificationLocked, "no line classification: PO may still choose one");

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
            var legacyName = new string('L', AssetNameRules.MaxLength + 20); // possible in the NVARCHAR(200) column
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
