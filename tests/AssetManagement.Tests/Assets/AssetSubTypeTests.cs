using System;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;
namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class AssetSubTypeNormalizerTests
    {
        [Test]
        public void NormalizeBrand_TrimsWhitespace()
        {
            Assert.AreEqual("HP", AssetSubTypeNormalizer.NormalizeBrand("  HP  "));
        }
        [Test]
        public void BrandModelEquals_IsCaseInsensitive()
        {
            Assert.IsTrue(AssetSubTypeNormalizer.BrandModelEquals("hp", "elitebook", "HP", "EliteBook"));
            Assert.IsFalse(AssetSubTypeNormalizer.BrandModelEquals("hp", "elitebook", "HP", "ProBook"));
        }
        [Test]
        public void BuildSuggestedName_CombinesBrandAndModel()
        {
            Assert.AreEqual("Dell - Latitude 7420", AssetSubTypeNormalizer.BuildSuggestedName("Dell", "Latitude 7420"));
        }

        [Test]
        public void NormalizeName_RepairsMojibakeEnDash()
        {
            Assert.AreEqual("Apple - MacBook", AssetSubTypeNormalizer.NormalizeName("Apple \u00E2\u20AC\u2019 MacBook"));
            Assert.AreEqual("Dell - Latitude 7420", AssetSubTypeNormalizer.NormalizeName("Dell \u2013 Latitude 7420"));
        }
    }
    [TestFixture]
    public class AssetSubTypeResolverTests
    {
        [Test]
        public void Resolve_MatchesExistingSubTypeByBrandAndModel()
        {
            var unitOfWork = new FakeUnitOfWork();
            SeedSubType(unitOfWork, id: 5, assetTypeId: 2, brand: "Dell", model: "Latitude 7420");
            var resolver = new AssetSubTypeResolver(TestServiceFactory.CreateAssetSubTypeService(unitOfWork));
            var result = resolver.Resolve(2, "dell", "latitude 7420", null);
            Assert.IsTrue(result.IsMatched);
            Assert.AreEqual(5, result.SubType.Id);
        }
        [Test]
        public void Resolve_ReturnsUnresolvedWhenNoBrandOrModel()
        {
            var unitOfWork = new FakeUnitOfWork();
            var resolver = new AssetSubTypeResolver(TestServiceFactory.CreateAssetSubTypeService(unitOfWork));
            var result = resolver.Resolve(2, "", "", null);
            Assert.IsFalse(result.IsMatched);
            Assert.IsFalse(result.RequiresAssignment);
        }
        [Test]
        public void Resolve_ReturnsUnresolvedWhenNoMatch()
        {
            var unitOfWork = new FakeUnitOfWork();
            var resolver = new AssetSubTypeResolver(TestServiceFactory.CreateAssetSubTypeService(unitOfWork));
            var result = resolver.Resolve(2, "Lenovo", "ThinkPad", null);
            Assert.IsTrue(result.RequiresAssignment);
            Assert.AreEqual("Lenovo", result.Brand);
            Assert.AreEqual("ThinkPad", result.Model);
        }
        private static void SeedSubType(FakeUnitOfWork unitOfWork, int id, int assetTypeId, string brand, string model)
        {
            unitOfWork.Seed(new AssetSubType
            {
                Id = id,
                AssetTypeId = assetTypeId,
                Name = brand + " - " + model,
                Brand = brand,
                Model = model,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
    [TestFixture]
    public class AssetStockServiceTests
    {
        [Test]
        public void GetAvailableQuantity_CountsAvailableAssets()
        {
            var unitOfWork = new FakeUnitOfWork();
            SeedSubType(unitOfWork, 10);
            unitOfWork.Seed(BuildAsset(1, 10, AssetStatus.InStore, departmentId: 3));
            unitOfWork.Seed(BuildAsset(2, 10, AssetStatus.Assigned, departmentId: 3));
            unitOfWork.Seed(BuildAsset(3, 10, AssetStatus.InStore, departmentId: 4));
            var service = TestServiceFactory.CreateAssetStockService(unitOfWork);
            Assert.AreEqual(1, service.GetAvailableQuantity(10, 3));
            Assert.AreEqual(2, service.GetAvailableQuantity(10, null));
        }
        private static void SeedSubType(FakeUnitOfWork unitOfWork, int id)
        {
            unitOfWork.Seed(new AssetSubType
            {
                Id = id,
                AssetTypeId = 1,
                Name = "Test item",
                Brand = "Brand",
                Model = "Model",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }
        private static Asset BuildAsset(int id, int subTypeId, AssetStatus status, int departmentId)
        {
            return new Asset
            {
                Id = id,
                AssetTag = "AST-" + id,
                AssetName = "Asset " + id,
                CategoryId = 1,
                AssetTypeId = 1,
                AssetSubTypeId = subTypeId,
                DepartmentId = departmentId,
                Currency = "USD",
                AcquisitionCost = 100,
                CurrentStatus = status,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
    [TestFixture]
    public class ReceivingServiceUnitTrackingTests
    {
        [Test]
        public void Receive_CreatesAssetsWithoutSerialNumbers()
        {
            var unitOfWork = SeedPurchase(quantity: 4);
            var service = TestServiceFactory.CreateReceivingService(unitOfWork);
            var result = service.Receive(new AssetReceiveVm
            {
                PurchaseRecordId = 100,
                AssetSubTypeId = 50,
                ReceivePlacementChoice = ReceivingService.PlacementCompanyCustody,
                ReceivedDate = DateTime.UtcNow,
                ConditionOnReceipt = "New",
                QuantityReceived = 4
            }, "receiver-1");
            Assert.AreEqual(4, result.CreatedAssets.Count);
            Assert.IsTrue(result.CreatedAssets.All(x => !string.IsNullOrWhiteSpace(x.AssetTag)));
            Assert.AreEqual(4, unitOfWork.Repository<AssetReceiving>().GetAll().Count(x => x.PurchaseRecordId == 100));
            var created = unitOfWork.Repository<Asset>().GetAll().Where(x => x.Id != 60).ToList();
            Assert.AreEqual(4, created.Count);
            Assert.IsTrue(created.All(x => string.IsNullOrWhiteSpace(x.SerialNumber)));
            Assert.IsTrue(created.Select(x => x.AssetTag).Distinct().Count() == 4);
        }
        [Test]
        public void Receive_AssignsDepartmentWhenOptedIn()
        {
            var unitOfWork = SeedPurchase(quantity: 2);
            var service = TestServiceFactory.CreateReceivingService(unitOfWork);
            service.Receive(new AssetReceiveVm
            {
                PurchaseRecordId = 100,
                AssetSubTypeId = 50,
                ReceivePlacementChoice = ReceivingService.PlacementRequisitionDepartment,
                AssignToRequisitionDepartment = true,
                ReceivedDate = DateTime.UtcNow,
                ConditionOnReceipt = "New",
                QuantityReceived = 2
            }, "receiver-1");
            var created = unitOfWork.Repository<Asset>().GetAll().Where(x => x.Id != 60).ToList();
            Assert.AreEqual(2, created.Count);
            Assert.IsTrue(created.All(x => x.DepartmentId == 5));
        }
        private static FakeUnitOfWork SeedPurchase(int quantity)
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Supplier { Id = 1, SupplierName = "Acme", CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new AssetCategory { Id = 1, Name = "IT", CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new AssetType { Id = 1, Name = "Laptop", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new Department { Id = 5, Name = "IT", Code = "IT", IsActive = true, IsRequisitionTarget = true });
            unitOfWork.Seed(new AssetSubType
            {
                Id = 50,
                AssetTypeId = 1,
                Name = "Dell Mouse",
                Brand = "Dell",
                Model = "MS116",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new Asset
            {
                Id = 60,
                AssetTag = "REF-MOUSE",
                AssetName = "Dell Mouse template",
                CategoryId = 1,
                AssetTypeId = 1,
                AssetSubTypeId = 50,
                Brand = "Dell",
                Model = "MS116",
                DepartmentId = 5,
                Currency = "USD",
                AcquisitionCost = 1,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new PurchaseRequest
            {
                Id = 1,
                DepartmentId = 5,
                TargetAssetId = 60,
                ItemDescription = "Dell mouse",
                Quantity = quantity,
                Currency = "USD",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            unitOfWork.Seed(new PurchaseRecord
            {
                Id = 100,
                PurchaseRequestId = 1,
                SupplierId = 1,
                Quantity = quantity,
                UnitCost = 5,
                TotalCost = 5 * quantity,
                PurchaseDate = DateTime.UtcNow,
                Currency = "USD",
                OrganizationId = 1,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            return unitOfWork;
        }
    }
}
