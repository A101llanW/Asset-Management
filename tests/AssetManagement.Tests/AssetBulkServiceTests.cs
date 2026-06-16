using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class AssetBulkServiceTests
    {
        [Test]
        public void Execute_WithoutPermission_Throws()
        {
            var asset = new Asset { Id = 1, AssetTag = "TAG-1", IsActive = true, CurrentStatus = AssetStatus.InStore };
            var unitOfWork = new Mock<IUnitOfWork>();
            var assetRepo = new Mock<IRepository<Asset>>();
            assetRepo.Setup(x => x.GetById(1)).Returns(asset);
            unitOfWork.Setup(x => x.Repository<Asset>()).Returns(assetRepo.Object);

            var auth = new Mock<IAuthorizationService>();
            auth.Setup(x => x.HasPermission("user-1", "Assets.Edit")).Returns(false);

            var service = TestServiceFactory.CreateAssetBulkService(unitOfWork.Object, authorization: auth.Object);
            var request = new AssetBulkActionRequestVm
            {
                AssetIds = new List<int> { 1 },
                Action = "department",
                TargetDepartmentId = 2,
                PermissionCodes = new List<string>()
            };

            var result = service.Execute(request, "user-1");

            Assert.AreEqual(0, result.ProcessedCount);
            Assert.AreEqual(1, result.SkippedCount);
            Assert.IsTrue(result.Messages.Any(m => m.IndexOf("permission", System.StringComparison.OrdinalIgnoreCase) >= 0));
        }

        [Test]
        public void Execute_ChangeDepartment_WithPermission_Processes()
        {
            var asset = new Asset { Id = 1, AssetTag = "TAG-1", IsActive = true, CurrentStatus = AssetStatus.InStore, DepartmentId = 1 };
            var unitOfWork = new Mock<IUnitOfWork>();
            var assetRepo = new Mock<IRepository<Asset>>();
            assetRepo.Setup(x => x.GetById(1)).Returns(asset);
            unitOfWork.Setup(x => x.Repository<Asset>()).Returns(assetRepo.Object);

            var auth = new Mock<IAuthorizationService>();
            auth.Setup(x => x.HasPermission("user-1", "Assets.Edit")).Returns(true);

            var service = TestServiceFactory.CreateAssetBulkService(unitOfWork.Object, authorization: auth.Object);
            var request = new AssetBulkActionRequestVm
            {
                AssetIds = new List<int> { 1 },
                Action = "department",
                TargetDepartmentId = 5,
                PermissionCodes = new List<string> { "Assets.Edit" }
            };

            var result = service.Execute(request, "user-1");

            Assert.AreEqual(1, result.ProcessedCount);
            Assert.AreEqual(5, asset.DepartmentId);
            unitOfWork.Verify(x => x.SaveChanges(), Times.Once());
        }
    }
}
