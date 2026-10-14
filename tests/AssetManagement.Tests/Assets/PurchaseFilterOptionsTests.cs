using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class PurchaseFilterOptionsTests
    {
        private static List<PurchaseRecordVm> Purchases()
        {
            return new List<PurchaseRecordVm>
            {
                new PurchaseRecordVm { Id = 5, PurchaseOrderNumber = "5001", SupplierName = "Acme", PurchaseDate = new DateTime(2026, 9, 1) },
                new PurchaseRecordVm { Id = 17, PurchaseOrderNumber = "5676", SupplierName = "Dell", PurchaseDate = new DateTime(2026, 10, 10) },
                new PurchaseRecordVm { Id = 9, PurchaseOrderNumber = null, SupplierName = null, PurchaseDate = new DateTime(2026, 10, 10) }
            };
        }

        [Test]
        public void Build_AllFirst_ThenNewestPurchaseFirst()
        {
            var items = PurchaseFilterOptions.Build(Purchases(), null);
            Assert.AreEqual(string.Empty, items[0].Value);
            Assert.IsTrue(items[0].Selected);
            CollectionAssert.AreEqual(new[] { "", "17", "9", "5" }, items.Select(i => i.Value).ToArray());
        }

        [Test]
        public void Build_SelectsRequestedPurchase()
        {
            var items = PurchaseFilterOptions.Build(Purchases(), 17);
            Assert.IsFalse(items[0].Selected);
            Assert.AreEqual("17", items.Single(i => i.Selected).Value);
        }

        [Test]
        public void Label_ShowsPoSupplierAndDate_FallsBackToId()
        {
            var list = Purchases();
            Assert.AreEqual("PO 5676 \u00b7 Dell \u00b7 10 Oct 2026", PurchaseFilterOptions.Label(list[1]));
            Assert.AreEqual("Purchase #9 \u00b7 10 Oct 2026", PurchaseFilterOptions.Label(list[2]));
        }

        [Test]
        public void ResolveTenantPurchaseId_RejectsIdsOutsideTenantList()
        {
            Assert.AreEqual(17, PurchaseFilterOptions.ResolveTenantPurchaseId(Purchases(), 17));
            Assert.IsNull(PurchaseFilterOptions.ResolveTenantPurchaseId(Purchases(), 999));
            Assert.IsNull(PurchaseFilterOptions.ResolveTenantPurchaseId(Purchases(), 0));
            Assert.IsNull(PurchaseFilterOptions.ResolveTenantPurchaseId(Purchases(), null));
            Assert.IsNull(PurchaseFilterOptions.ResolveTenantPurchaseId(null, 17));
        }
    }
}