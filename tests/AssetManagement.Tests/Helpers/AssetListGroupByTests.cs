using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class AssetListGroupByTests
    {
        [Test]
        public void Normalize_ReturnsKnownDimensions()
        {
            Assert.AreEqual(AssetListGroupBy.Department, AssetListGroupBy.Normalize("department"));
            Assert.AreEqual(AssetListGroupBy.Category, AssetListGroupBy.Normalize("category"));
            Assert.AreEqual(AssetListGroupBy.Type, AssetListGroupBy.Normalize("type"));
            Assert.AreEqual(AssetListGroupBy.SubType, AssetListGroupBy.Normalize("subtype"));
            Assert.AreEqual(AssetListGroupBy.Status, AssetListGroupBy.Normalize("status"));
        }

        [Test]
        public void Normalize_ReturnsProduct_ForUnknownOrEmptyValues()
        {
            Assert.AreEqual(AssetListGroupBy.Product, AssetListGroupBy.Normalize(null));
            Assert.AreEqual(AssetListGroupBy.Product, AssetListGroupBy.Normalize(""));
            Assert.AreEqual(AssetListGroupBy.Product, AssetListGroupBy.Normalize("custodian"));
        }

        [Test]
        public void IsSimpleDimension_IsTrueForNonProductModes()
        {
            Assert.IsTrue(AssetListGroupBy.IsSimpleDimension(AssetListGroupBy.Category));
            Assert.IsFalse(AssetListGroupBy.IsSimpleDimension(AssetListGroupBy.Product));
        }

        [Test]
        public void ResolveFromRequest_PrefersFilterGroupBy_WhenMvcBindsQueryToFilter()
        {
            var filter = new AssetFilterVm { GroupBy = "department" };
            Assert.AreEqual(AssetListGroupBy.Department, AssetListGroupBy.ResolveFromRequest(filter, "product"));
        }

        [Test]
        public void ResolveFromRequest_UsesParam_WhenFilterGroupByMissing()
        {
            Assert.AreEqual(AssetListGroupBy.Category, AssetListGroupBy.ResolveFromRequest(new AssetFilterVm(), "category"));
        }

        [Test]
        public void Options_IncludesAllSupportedModes()
        {
            Assert.AreEqual(6, AssetListGroupBy.Options.Length);
        }
    }
}
