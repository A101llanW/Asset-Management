using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class AssetListGroupKeyTests
    {
        [Test]
        public void TryParse_ReturnsCategoryParts()
        {
            AssetListGroupKeyParts parts;
            Assert.IsTrue(AssetListGroupKey.TryParse("cat|12", out parts));
            Assert.AreEqual(AssetListGroupBy.Category, parts.GroupBy);
            Assert.AreEqual(12, parts.CategoryId);
        }

        [Test]
        public void TryParse_ReturnsDepartmentParts_WithNullDepartment()
        {
            AssetListGroupKeyParts parts;
            Assert.IsTrue(AssetListGroupKey.TryParse("dept|0", out parts));
            Assert.AreEqual(AssetListGroupBy.Department, parts.GroupBy);
            Assert.IsNull(parts.DepartmentId);
        }

        [Test]
        public void TryParse_ReturnsStatusParts()
        {
            AssetListGroupKeyParts parts;
            Assert.IsTrue(AssetListGroupKey.TryParse("status|5", out parts));
            Assert.AreEqual(AssetListGroupBy.Status, parts.GroupBy);
            Assert.AreEqual(AssetStatus.InStore, parts.Status);
        }

        [Test]
        public void TryParse_ReturnsFalse_ForProductKey()
        {
            AssetListGroupKeyParts parts;
            Assert.IsFalse(AssetListGroupKey.TryParse("Chair|1|2|3", out parts));
        }
    }
}
