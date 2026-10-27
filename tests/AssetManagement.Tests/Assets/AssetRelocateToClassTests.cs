using System;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class AssetRelocateToClassTests
    {
        private const string ActorUserId = "user-relocate";

        [Test]
        public void RelocateToClassDepartment_WithoutPermission_Throws()
        {
            var unitOfWork = SeedRelocateScenario(sourceDepartmentId: 1, targetDepartmentId: 2);
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new DenyAllAuthorizationService());

            var ex = Assert.Throws<BusinessException>(() =>
                service.RelocateToClassDepartment(1, 2, ActorUserId));

            StringAssert.Contains("permission", ex.Message);
        }

        [Test]
        public void RelocateToClassDepartment_WithAssetsEditPermission_UpdatesDepartment()
        {
            var unitOfWork = SeedRelocateScenario(sourceDepartmentId: 1, targetDepartmentId: 2);
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            // Allan, 2026-10-27 (QA P3.6/X9.4): classroom to classroom is allowed.
            service.RelocateToClassDepartment(1, 2, ActorUserId);
            Assert.AreEqual(2, unitOfWork.Repository<Asset>().GetById(1).DepartmentId);
            var custodyEvent = unitOfWork.Repository<AssetCustodyEvent>().GetAll().Single();
            Assert.AreEqual(1, custodyEvent.FromDepartmentId);
            Assert.AreEqual(2, custodyEvent.ToDepartmentId);
        }

        [Test]
        public void RelocateToClassDepartment_WithAssetsTransferPermission_AllowsCrossDepartmentScope()
        {
            var unitOfWork = SeedRelocateScenario(sourceDepartmentId: 1, targetDepartmentId: 2);
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                departmentScope: new StrictDepartmentScopeService(1),
                authorization: new FixedPermissionAuthorizationService("Assets.Transfer"));

            // Transfer permission still bypasses department scope (no 'outside your scope' error); class to class is allowed.
            service.RelocateToClassDepartment(1, 2, ActorUserId);
            Assert.AreEqual(2, unitOfWork.Repository<Asset>().GetById(1).DepartmentId);
        }

        [Test]
        public void RelocateToClassDepartment_FromARoom_IsStillRefused()
        {
            var unitOfWork = SeedRelocateScenario(sourceDepartmentId: 1, targetDepartmentId: 2);
            unitOfWork.Repository<Department>().GetById(1).DepartmentKind = DepartmentKind.Room;
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var ex = Assert.Throws<BusinessException>(() => service.RelocateToClassDepartment(1, 2, ActorUserId));
            Assert.AreEqual(AssetMoveRules.RefusedMessage, ex.Message);
            Assert.AreEqual(1, unitOfWork.Repository<Asset>().GetById(1).DepartmentId);
        }

        [Test]
        public void MoveRule_ClassToClassAndClassToRoom_OnlyClassroomsCanTakeClassTargets()
        {
            Assert.IsTrue(AssetMoveRules.IsAllowed(DepartmentKind.Class, DepartmentKind.Class));
            Assert.IsTrue(AssetMoveRules.IsAllowed(DepartmentKind.Class, DepartmentKind.Room));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Class, DepartmentKind.SubDepartment));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Room, DepartmentKind.Class));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.SubDepartment, DepartmentKind.Class));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Grade, DepartmentKind.Class));
            Assert.IsTrue(AssetMoveRules.CanMoveToClassFrom(DepartmentKind.Class));
            Assert.IsFalse(AssetMoveRules.CanMoveToClassFrom(DepartmentKind.Room));
            Assert.IsTrue(AssetMoveRules.CanMoveToClassFrom(null), "company custody placement is not a move");
        }

        [Test]
        public void RelocateToClassDepartment_WithoutTransferPermission_EnforcesDepartmentScope()
        {
            var unitOfWork = SeedRelocateScenario(sourceDepartmentId: 1, targetDepartmentId: 2);
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                departmentScope: new StrictDepartmentScopeService(1),
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var ex = Assert.Throws<BusinessException>(() =>
                service.RelocateToClassDepartment(1, 2, ActorUserId));

            StringAssert.Contains("outside your scope", ex.Message);
        }

        [Test]
        public void RelocateToClassDepartment_NonClassTarget_Throws()
        {
            var unitOfWork = SeedRelocateScenario(sourceDepartmentId: 1, targetDepartmentId: 3, targetKind: DepartmentKind.Administrative);
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var ex = Assert.Throws<BusinessException>(() =>
                service.RelocateToClassDepartment(1, 3, ActorUserId));

            StringAssert.Contains("class", ex.Message);
        }

        [Test]
        public void RelocateToClassDepartment_AssignedAsset_Throws()
        {
            var unitOfWork = SeedRelocateScenario(
                sourceDepartmentId: 1,
                targetDepartmentId: 2,
                status: AssetStatus.Assigned,
                custodianId: "custodian-1");
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var ex = Assert.Throws<BusinessException>(() =>
                service.RelocateToClassDepartment(1, 2, ActorUserId));

            StringAssert.Contains("custodian", ex.Message);
        }

        [Test]
        public void RelocateToClassDepartment_NonInStoreStatus_Throws()
        {
            var unitOfWork = SeedRelocateScenario(
                sourceDepartmentId: 1,
                targetDepartmentId: 2,
                status: AssetStatus.Disposed);
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var ex = Assert.Throws<BusinessException>(() =>
                service.RelocateToClassDepartment(1, 2, ActorUserId));

            StringAssert.Contains("cannot be moved", ex.Message);
        }

        private static FakeUnitOfWork SeedRelocateScenario(
            int sourceDepartmentId,
            int targetDepartmentId,
            DepartmentKind targetKind = DepartmentKind.Class,
            AssetStatus status = AssetStatus.InStore,
            string custodianId = null)
        {
            var unitOfWork = new FakeUnitOfWork();
            var now = DateTime.UtcNow;

            unitOfWork.Seed(new Department
            {
                Id = sourceDepartmentId,
                Name = "Class A",
                Code = "CLS-A",
                DepartmentKind = DepartmentKind.Class,
                CreatedAt = now,
                IsActive = true
            });

            unitOfWork.Seed(new Department
            {
                Id = targetDepartmentId,
                Name = targetKind == DepartmentKind.Class ? "Class B" : "Admin Office",
                Code = targetKind == DepartmentKind.Class ? "CLS-B" : "ADM",
                DepartmentKind = targetKind,
                CreatedAt = now,
                IsActive = true
            });

            unitOfWork.Seed(new Asset
            {
                Id = 1,
                AssetTag = "TAG-001",
                AssetName = "Chair",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = sourceDepartmentId,
                Currency = "USD",
                AcquisitionCost = 100m,
                CurrentStatus = status,
                CurrentCustodianId = custodianId,
                PurchaseDate = now,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = now,
                UsefulLifeMonths = 36,
                CreatedAt = now,
                IsActive = true
            });

            return unitOfWork;
        }
    }
}
