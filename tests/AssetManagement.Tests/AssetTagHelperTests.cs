using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class AssetTagHelperTests
    {
        [Test]
        public void ResolveTypeAbbreviation_UsesKnownDemoMappings()
        {
            Assert.AreEqual("LTP", AssetTagHelper.ResolveTypeAbbreviation("Laptop"));
            Assert.AreEqual("VHC", AssetTagHelper.ResolveTypeAbbreviation("Vehicle"));
            Assert.AreEqual("DESK", AssetTagHelper.ResolveTypeAbbreviation("Office Desk"));
        }

        [Test]
        public void GetNextSequence_IncrementsWithinPrefix()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 1,
                AssetTag = "IT-LTP-001",
                IsActive = true,
                DepartmentId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = System.DateTime.UtcNow
            });
            unitOfWork.Seed(new Asset
            {
                Id = 2,
                AssetTag = "IT-LTP-003",
                IsActive = true,
                DepartmentId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = System.DateTime.UtcNow
            });

            var assets = unitOfWork.Repository<Asset>().Query().Where(x => x.IsActive);
            var next = AssetTagHelper.GetNextSequence(assets, "IT-LTP-");

            Assert.AreEqual(4, next);
        }

        [Test]
        public void GenerateNextTag_BuildsDemoStyleTag()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 1,
                AssetTag = "IT-LTP-001",
                IsActive = true,
                DepartmentId = 1,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = System.DateTime.UtcNow
            });

            var assets = unitOfWork.Repository<Asset>().Query().Where(x => x.IsActive);
            var tag = AssetTagHelper.GenerateNextTag(assets, "IT", "Laptop");

            Assert.AreEqual("IT-LTP-002", tag);
        }
    }
}
