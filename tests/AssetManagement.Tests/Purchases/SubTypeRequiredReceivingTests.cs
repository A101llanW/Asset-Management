using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Purchases
{
    /// <summary>Sub-type required at receiving, no bindable defer flag, "Missing sub-type" asset filter, over-ordered requisitions.</summary>
    [TestFixture]
    public class SubTypeRequiredReceivingTests
    {
        private static FakeUnitOfWork Seed()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetCategory { Id = 1, Name = "IT", CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 1, Name = "Laptop", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 2, Name = "Printer", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            uow.Seed(new AssetType { Id = 3, Name = "Desk", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true }); // no sub-types
            uow.Seed(new AssetSubType { Id = 10, AssetTypeId = 1, Name = "Dell Latitude 5440", Brand = "Dell", Model = "Latitude 5440", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetSubType { Id = 20, AssetTypeId = 2, Name = "HP LaserJet", Brand = "HP", Model = "LaserJet", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new Department { Id = 1, Name = "Administration", Code = "ADM", DepartmentKind = DepartmentKind.Administrative, CreatedAt = DateTime.UtcNow, IsActive = true, IsRequisitionTarget = true });
            uow.Seed(new PurchaseRequest
            {
                Id = 100, RequestNumber = "PR-100", DepartmentId = 1, ApprovalStatus = ApprovalStatus.Approved, ItemDescription = "Laptops and printers",
                Quantity = 5, Currency = "KES", RequestedById = "requester", IsActive = true, CreatedAt = DateTime.UtcNow
            });
            uow.Seed(new PurchaseRequestLine { Id = 1, PurchaseRequestId = 100, LineNumber = 1, Description = "Laptop", Quantity = 3, IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new PurchaseRequestLine { Id = 2, PurchaseRequestId = 100, LineNumber = 2, Description = "Printer", Quantity = 2, IsActive = true, CreatedAt = DateTime.UtcNow });
            return uow;
        }

        /// <summary>Line 1: laptop type set on the PO WITHOUT a sub-type; line 2: printer, no classification.</summary>
        private static int CreateTypeOnlyOrder(FakeUnitOfWork uow)
        {
            return TestServiceFactory.CreatePurchaseService(uow).Create(new PurchaseRecordVm
            {
                PurchaseRequestId = 100, PurchaseOrderNumber = "PO-1", SupplierId = 1, PurchaseDate = DateTime.UtcNow, Currency = "KES",
                Lines = new List<PurchaseOrderLineInputVm>
                {
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 1, Quantity = 2, UnitCost = 1000m, AssetTypeId = 1, AssetSubTypeId = null },
                    new PurchaseOrderLineInputVm { PurchaseRequestLineId = 2, Quantity = 1, UnitCost = 500m }
                }
            });
        }

        private static ReceiveOrderLineVm Line(int lineId, int qty, int? overrideType = null, int? overrideSubType = null)
        {
            return new ReceiveOrderLineVm
            {
                PurchaseOrderLineId = lineId, QuantityReceived = qty, Condition = "New", TagMode = ReceiveOrderLineVm.TagModeGenerate,
                DestinationKind = ReceiveOrderLineVm.DestinationStore, OverrideAssetTypeId = overrideType, OverrideAssetSubTypeId = overrideSubType
            };
        }

        private static ReceiveOrderVm Wrap(int poId, params ReceiveOrderLineVm[] lines)
        {
            return new ReceiveOrderVm { PurchaseRecordId = poId, ReceivedDate = DateTime.UtcNow, Lines = lines.ToList() };
        }

        // ---------------- sub-type required at receiving ----------------

        [Test]
        public void Receiving_SourceTypeWithoutSubType_RejectsBlankSubType_AndAcceptsOneOfThatType()
        {
            var uow = Seed();
            var poId = CreateTypeOnlyOrder(uow);
            var laptopLine = uow.Repository<PurchaseOrderLine>().GetAll().First(x => x.PurchaseRecordId == poId && x.LineNumber == 1).Id;
            var receiving = TestServiceFactory.CreateReceivingService(uow);

            var screen = receiving.GetReceiveOrder(poId);
            Assert.IsFalse(screen.Lines[0].NeedsClassification, "type comes from the PO line");
            Assert.IsTrue(screen.Lines[0].NeedsSubType, "receiver must choose the sub-type");
            Assert.IsFalse(screen.Lines[0].TypeHasNoSubTypes);

            var ex = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(Wrap(poId, Line(laptopLine, 1)), "r"));
            StringAssert.Contains("sub-type is required", ex.Message);
            Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(Wrap(poId, Line(laptopLine, 1, overrideSubType: 20)), "r"), "sub-type of another type");
            Assert.AreEqual(0, uow.Repository<Asset>().GetAll().Count(), "nothing created");
            Assert.AreEqual(0, uow.Repository<AssetReceiving>().GetAll().Count());

            var result = receiving.ReceiveOrder(Wrap(poId, Line(laptopLine, 1, overrideType: 2, overrideSubType: 10)), "r");
            var asset = uow.Repository<Asset>().GetById(result.CreatedAssets.Single().AssetId);
            Assert.AreEqual(1, asset.AssetTypeId, "posted type still ignored when the source has one");
            Assert.AreEqual(10, asset.AssetSubTypeId);
        }

        [Test]
        public void Receiving_OverrideClassification_RejectsBlankSubType()
        {
            var uow = Seed();
            var poId = CreateTypeOnlyOrder(uow);
            var printerLine = uow.Repository<PurchaseOrderLine>().GetAll().First(x => x.PurchaseRecordId == poId && x.LineNumber == 2).Id;
            var receiving = TestServiceFactory.CreateReceivingService(uow);

            var ex = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(Wrap(poId, Line(printerLine, 1, overrideType: 2)), "r"));
            StringAssert.Contains("sub-type is required", ex.Message);
            Assert.AreEqual(0, uow.Repository<Asset>().GetAll().Count());

            var result = receiving.ReceiveOrder(Wrap(poId, Line(printerLine, 1, overrideType: 2, overrideSubType: 20)), "r");
            Assert.AreEqual(20, uow.Repository<Asset>().GetById(result.CreatedAssets.Single().AssetId).AssetSubTypeId);
        }

        [Test]
        public void Receiving_TypeWithNoSubTypesDefined_IsRejectedUntilOneIsAdded()
        {
            // Merged with live stage (d) / migration 101 trigger: no exemption, every received asset needs a sub-type.
            var uow = Seed();
            uow.Seed(new PurchaseRecord { Id = 77, SupplierId = 1, Quantity = 1, UnitCost = 10m, TotalCost = 10m, PurchaseDate = DateTime.UtcNow, Currency = "KES", IsActive = true, CreatedAt = DateTime.UtcNow });
            var receiving = TestServiceFactory.CreateReceivingService(uow);
            var ex = Assert.Throws<BusinessException>(() => receiving.ReceiveOrder(Wrap(77, Line(0, 1, overrideType: 3)), "r"));
            StringAssert.Contains("no active sub-types", ex.Message);
            Assert.AreEqual(0, uow.Repository<Asset>().GetAll().Count());
        }

        [Test]
        public void LegacyReceive_RejectsBlankSubType_AndStampsReceivedDateWhenComplete()
        {
            var uow = Seed();
            uow.Seed(new PurchaseRecord { Id = 78, SupplierId = 1, Quantity = 1, UnitCost = 10m, TotalCost = 10m, PurchaseDate = DateTime.UtcNow, Currency = "KES", IsActive = true, CreatedAt = DateTime.UtcNow });
            var receiving = TestServiceFactory.CreateReceivingService(uow);
            var received = new DateTime(2026, 10, 9, 0, 0, 0, DateTimeKind.Utc);
            Func<int?, AssetReceiveVm> model = sub => new AssetReceiveVm
            {
                PurchaseRecordId = 78, ReceivedDate = received, QuantityReceived = 1, ConditionOnReceipt = "New",
                AssetTypeId = 1, AssetSubTypeId = sub, ReceivePlacementChoice = ReceivingService.PlacementCompanyCustody
            };

            var ex = Assert.Throws<BusinessException>(() => receiving.Receive(model(null), "r"));
            StringAssert.Contains("sub-type", ex.Message);
            Assert.AreEqual(0, uow.Repository<Asset>().GetAll().Count());
            Assert.Throws<BusinessException>(() => receiving.Receive(model(20), "r"), "sub-type of another type");

            receiving.Receive(model(10), "r");
            Assert.AreEqual(10, uow.Repository<Asset>().GetAll().Single().AssetSubTypeId);
            Assert.AreEqual(received, uow.Repository<PurchaseRecord>().GetById(78).ReceivedDate, "fully received PO is stamped like ReceiveOrder");
        }

        // ---------------- crafted AllowDeferredSubTypeClassification is ignored ----------------

        [Test]
        public void CraftedDeferFlag_IsNotBindable_AndCreateStillRequiresTheSubType()
        {
            Assert.IsNull(typeof(AssetCreateVm).GetProperty("AllowDeferredSubTypeClassification"), "no bindable defer flag on the create/edit model");
            Assert.IsNull(typeof(AssetEditVmProbe).BaseType.GetProperty("AllowDeferredSubTypeClassification"));

            var uow = Seed();
            var assets = TestServiceFactory.CreateAssetService(uow);
            Func<int, AssetCreateVm> blank = typeId => new AssetCreateVm
            {
                AssetName = "Crafted", CategoryId = 1, AssetTypeId = typeId, AssetSubTypeId = null,
                PurchaseDate = DateTime.UtcNow, Currency = "KES", CurrentStatus = AssetStatus.InStore
            };

            // A crafted POST (e.g. AllowDeferredSubTypeClassification=true) has nothing to bind to: Create refuses a blank sub-type.
            Assert.Throws<BusinessException>(() => assets.Create(blank(1)));
            Assert.Throws<BusinessException>(() => assets.Create(blank(3)), "manual create always needs a sub-type");
            // The service-internal receiving entry point only waives it for a type that has no sub-types.
            Assert.Throws<BusinessException>(() => assets.CreateReceived(blank(1)));
            var id = assets.CreateReceived(blank(3));
            Assert.IsNull(uow.Repository<Asset>().GetById(id).AssetSubTypeId);
        }

        private class AssetEditVmProbe : AssetCreateVm
        {
        }

        // ---------------- "Missing sub-type" asset list filter ----------------

        [Test]
        public void AssetList_MissingSubTypeFilter_AddsTheIsNullClause_InListAndRoomViews()
        {
            var off = AssetQueryService.BuildWhereClause(new AssetFilterVm());
            StringAssert.DoesNotContain(AssetQueryService.MissingSubTypeClause, off);

            var filter = new AssetFilterVm { MissingSubTypeOnly = true, CategoryId = 1 };
            var on = AssetQueryService.BuildWhereClause(filter);
            StringAssert.Contains("a.[AssetSubTypeId] IS NULL", on);
            StringAssert.Contains("a.[CategoryId] = @CategoryId", on);
            StringAssert.Contains("a.[AssetSubTypeId] IS NULL", AssetRoomGroupQueryService.BuildRoomWhereClause(filter), "room view uses the same filters");
        }

        // ---------------- over-ordered requisitions ----------------

        [Test]
        public void OverOrderedRequisition_IsFlaggedOnTheRequisitionSummaryAndTheReceiveScreen()
        {
            Assert.IsTrue(PurchaseOrderQuantityRules.IsOverOrdered(1, 2), "PR 31 shape: 1 requested, 2 ordered");
            Assert.IsFalse(PurchaseOrderQuantityRules.IsOverOrdered(2, 2));
            Assert.IsFalse(PurchaseOrderQuantityRules.IsOverOrdered(5, 3));

            var uow = Seed();
            var normal = CreateTypeOnlyOrder(uow); // 3 of 5
            var purchases = TestServiceFactory.CreatePurchaseService(uow);
            Assert.IsFalse(purchases.GetRequisitionOrderSummary(100).IsOverOrdered);
            Assert.IsFalse(TestServiceFactory.CreateReceivingService(uow).GetReceiveOrder(normal).IsRequisitionOverOrdered);

            // A legacy PO recorded before the over-order guard (like PR 31's PO): 4 more units, total 7 of 5.
            uow.Seed(new PurchaseRecord { Id = 90, PurchaseRequestId = 100, SupplierId = 1, Quantity = 4, UnitCost = 10m, TotalCost = 40m, PurchaseDate = DateTime.UtcNow, Currency = "KES", IsActive = true, CreatedAt = DateTime.UtcNow });
            var summary = purchases.GetRequisitionOrderSummary(100);
            Assert.IsTrue(summary.IsOverOrdered);
            Assert.AreEqual(2, summary.OverOrderedBy);
            var screen = TestServiceFactory.CreateReceivingService(uow).GetReceiveOrder(90);
            Assert.IsTrue(screen.IsRequisitionOverOrdered);
            Assert.AreEqual(5, screen.RequisitionQuantity);
            Assert.AreEqual(7, screen.RequisitionOrderedQuantity);

            uow.Seed(new PurchaseRecord { Id = 91, SupplierId = 1, Quantity = 4, UnitCost = 10m, TotalCost = 40m, PurchaseDate = DateTime.UtcNow, Currency = "KES", IsActive = true, CreatedAt = DateTime.UtcNow });
            Assert.IsFalse(TestServiceFactory.CreateReceivingService(uow).GetReceiveOrder(91).IsRequisitionOverOrdered, "no requisition, no flag");
        }
    }
}