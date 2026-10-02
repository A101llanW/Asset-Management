using System;
using System.Collections.Generic;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assignments
{
    [TestFixture]
    public class BatchAssignServiceTests
    {
        [Test]
        public void BatchAssign_AssignsMultipleAssetsAndReturnsPerRowResults()
        {
            var unitOfWork = new FakeUnitOfWork();
            SeedAssignableAsset(unitOfWork, 1, "AST-001");
            SeedAssignableAsset(unitOfWork, 2, "AST-002");

            var users = new FakeUserService();
            users.Seed(new UserVm
            {
                Id = "user-1",
                FirstName = "Alex",
                LastName = "Admin",
                DepartmentId = 1,
                IsActive = true
            });
            users.Seed(new UserVm
            {
                Id = "user-2",
                FirstName = "Bailey",
                LastName = "User",
                DepartmentId = 1,
                IsActive = true
            });

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork, users);
            var result = service.BatchAssign(new BatchAssignRequestVm
            {
                HandedOverById = "admin-1",
                ToDepartmentId = 1,
                Items = new List<BatchAssignItemVm>
                {
                    new BatchAssignItemVm { AssetId = 1, ToUserId = "user-1" },
                    new BatchAssignItemVm { AssetId = 2, ToUserId = "user-2" }
                }
            });

            Assert.AreEqual(2, result.ProcessedCount);
            Assert.AreEqual(0, result.SkippedCount);
            Assert.AreEqual(2, result.Rows.Count);
            Assert.IsTrue(result.Rows[0].Success);
            Assert.IsTrue(result.Rows[1].Success);

            var assetOne = unitOfWork.Repository<Asset>().GetById(1);
            var assetTwo = unitOfWork.Repository<Asset>().GetById(2);
            Assert.AreEqual("user-1", assetOne.CurrentCustodianId);
            Assert.AreEqual("user-2", assetTwo.CurrentCustodianId);
            Assert.AreEqual(AssetStatus.Assigned, assetOne.CurrentStatus);
            Assert.AreEqual(AssetStatus.Assigned, assetTwo.CurrentStatus);
        }

        [Test]
        public void BatchAssign_ReturnsRowFailureWithoutStoppingOtherRows()
        {
            var unitOfWork = new FakeUnitOfWork();
            SeedAssignableAsset(unitOfWork, 1, "AST-001");
            unitOfWork.Seed(new Asset
            {
                Id = 2,
                AssetTag = "AST-002",
                AssetName = "Assigned Laptop",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 1000,
                CurrentStatus = AssetStatus.Assigned,
                CurrentCustodianId = "existing-user",
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                CreatedAt = DateTime.UtcNow
            });

            var users = new FakeUserService();
            users.Seed(new UserVm
            {
                Id = "user-1",
                FirstName = "Alex",
                LastName = "Admin",
                DepartmentId = 1,
                IsActive = true
            });

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork, users);
            var result = service.BatchAssign(new BatchAssignRequestVm
            {
                HandedOverById = "admin-1",
                ToDepartmentId = 1,
                Items = new List<BatchAssignItemVm>
                {
                    new BatchAssignItemVm { AssetId = 1, ToUserId = "user-1" },
                    new BatchAssignItemVm { AssetId = 2, ToUserId = "user-1" }
                }
            });

            Assert.AreEqual(1, result.ProcessedCount);
            Assert.AreEqual(1, result.SkippedCount);
            Assert.IsTrue(result.Rows[0].Success);
            Assert.IsFalse(result.Rows[1].Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.Rows[1].Message));
        }

        [Test]
        public void BatchAssign_SkipsRowsWithoutCustodianSelection()
        {
            var unitOfWork = new FakeUnitOfWork();
            SeedAssignableAsset(unitOfWork, 1, "AST-001");

            var service = TestServiceFactory.CreateAssignmentService(unitOfWork);
            var result = service.BatchAssign(new BatchAssignRequestVm
            {
                HandedOverById = "admin-1",
                Items = new List<BatchAssignItemVm>
                {
                    new BatchAssignItemVm { AssetId = 1, ToUserId = null }
                }
            });

            Assert.AreEqual(0, result.ProcessedCount);
            Assert.AreEqual(1, result.SkippedCount);
            Assert.IsFalse(result.Rows[0].Success);
        }

        private static void SeedAssignableAsset(FakeUnitOfWork unitOfWork, int id, string tag)
        {
            unitOfWork.Seed(new Asset
            {
                Id = id,
                AssetTag = tag,
                AssetName = "Laptop",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 1000,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}
