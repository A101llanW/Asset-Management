using System;
using System.Linq;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using AssetManagement.Tests.Requisitions;
using NUnit.Framework;

namespace AssetManagement.Tests.Suppliers
{
    /// <summary>
    /// QA 2026-10-28 finding 4 (Allan 09:33 EAT "Fix"): choosing supplier Techdep filled only PO line 1. The comparison
    /// now carries one result per requisition line and purchase-create.js applies the supplier to every line.
    /// </summary>
    [TestFixture]
    public class SupplierLinePriceComparisonTests
    {
        private const int Techdep = 7;
        private const int Acme = 8;

        private static FakeUnitOfWork Seed()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Supplier { Id = Techdep, SupplierName = "Techdep", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new Supplier { Id = Acme, SupplierName = "Acme", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetCategory { Id = 141, Name = "ICT", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetType { Id = 582, Name = "Desktop", AssetCategoryId = 141, IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new AssetType { Id = 583, Name = "Printer", AssetCategoryId = 141, IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new SupplierCatalogItem { Id = 6, SupplierId = Techdep, ItemName = "Desktop computer", AssetCategoryId = 141, AssetTypeId = 582, UnitPrice = 1.24m, Currency = "KES", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new SupplierCatalogItem { Id = 9, SupplierId = Acme, ItemName = "Desktop computer", AssetCategoryId = 141, AssetTypeId = 582, UnitPrice = 2.50m, Currency = "KES", IsActive = true, CreatedAt = DateTime.UtcNow });

            // History: Techdep sold toner at 1,500 on an older requisition (the historical-average fallback).
            uow.Seed(new PurchaseRequest { Id = 40, RequestNumber = "PR-40", DepartmentId = 1, ApprovalStatus = ApprovalStatus.Approved, ItemDescription = "Toner cartridges", Quantity = 1, Currency = "KES", RequestedById = "u", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new PurchaseRecord { Id = 1, PurchaseRequestId = 40, SupplierId = Techdep, Quantity = 1, UnitCost = 1500m, TotalCost = 1500m, Currency = "KES", PurchaseDate = DateTime.UtcNow, IsActive = true, CreatedAt = DateTime.UtcNow });

            uow.Seed(new PurchaseRequest { Id = 65, RequestNumber = "PR-65", DepartmentId = 1, ApprovalStatus = ApprovalStatus.Approved, ItemDescription = "Desktop computer; Toner cartridges; Chairs", Quantity = 4, Currency = "KES", RequestedById = "u", IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new PurchaseRequestLine { Id = 651, PurchaseRequestId = 65, LineNumber = 1, ItemName = "Desktop computer", Description = "For the lab", Quantity = 2, AssetCategoryId = 141, AssetTypeId = 582, IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new PurchaseRequestLine { Id = 652, PurchaseRequestId = 65, LineNumber = 2, ItemName = "Toner", Description = "Toner cartridges", Quantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new PurchaseRequestLine { Id = 653, PurchaseRequestId = 65, LineNumber = 3, ItemName = "Stool", Description = "Wooden", Quantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow });
            uow.Seed(new PurchaseRequestLine { Id = 654, PurchaseRequestId = 65, LineNumber = 4, ItemName = "Old line", Description = "Removed", Quantity = 1, IsActive = false, CreatedAt = DateTime.UtcNow });
            return uow;
        }

        private static SupplierCatalogService Service(FakeUnitOfWork uow)
        {
            return new SupplierCatalogService(uow, new FakeOrganizationScopeService());
        }

        [Test]
        public void Comparison_HasOneResultPerActiveRequisitionLine_InLineOrder()
        {
            var result = Service(Seed()).GetPriceComparison(65, null);

            CollectionAssert.AreEqual(new[] { 651, 652, 653 }, result.Lines.Select(x => x.PurchaseRequestLineId).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, result.Lines.Select(x => x.LineNumber).ToArray());
            Assert.AreEqual("Desktop computer For the lab", result.Lines[0].ItemDescription);
        }

        [Test]
        public void EachLine_GetsItsOwnSupplierPrices_CatalogFirstThenHistory()
        {
            var result = Service(Seed()).GetPriceComparison(65, null);

            var desktop = result.Lines[0];
            Assert.IsTrue(desktop.HasCatalogMatches);
            Assert.AreEqual(1.24m, desktop.Rows.Single(x => x.SupplierId == Techdep).UnitPrice);
            Assert.AreEqual(2.50m, desktop.Rows.Single(x => x.SupplierId == Acme).UnitPrice);
            Assert.IsTrue(desktop.Rows[0].IsCheapest);

            var toner = result.Lines[1];
            Assert.IsFalse(toner.HasCatalogMatches);
            Assert.IsTrue(toner.HasHistoricalFallback);
            Assert.AreEqual(1500m, toner.Rows.Single(x => x.SupplierId == Techdep).UnitPrice);

            CollectionAssert.IsEmpty(result.Lines[2].Rows, "no supplier has a price for the stool line");
        }

        [Test]
        public void RequisitionLevelRows_AreUnchanged()
        {
            var result = Service(Seed()).GetPriceComparison(65, null);

            Assert.IsTrue(result.HasCatalogMatches);
            Assert.AreEqual("Desktop computer; Toner cartridges; Chairs", result.ItemDescription);
            Assert.AreEqual(2, result.Rows.Count);
            Assert.AreEqual("KES", result.Currency);
        }

        [Test]
        public void NoRequisition_NoLines()
        {
            var result = Service(Seed()).GetPriceComparison(null, "Desktop");
            CollectionAssert.IsEmpty(result.Lines);
            Assert.IsTrue(result.Rows.Count > 0);
        }

        [Test]
        public void Script_AppliesTheSupplierToEveryPoLine_NotOnlyUnitCost()
        {
            var js = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Scripts", "app", "purchase-create.js");
            Assert.IsFalse(js.Contains("#UnitCost"), "no single-line #UnitCost logic left");
            StringAssert.Contains("poRows().each(function () {", js);
            StringAssert.Contains("input[name$=\".PurchaseRequestLineId\"]", js);
            StringAssert.Contains("input.attr(EDITED) !== 'true'", js);
            QaSixRequisitionFormTests.AssertIe11Safe(js);

            var lines = QaSixRequisitionFormTests.Read("src", "AssetManagement.Web", "Scripts", "app", "purchase-order-lines.js");
            StringAssert.Contains("table.addEventListener(\"am:po-recalc\"", lines);
            Assert.IsFalse(lines.Contains("getElementById(\"UnitCost\")"));
        }
    }
}
