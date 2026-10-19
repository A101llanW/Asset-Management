using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assignments
{
    /// <summary>rel1009h: receive into store, batch pool rows, top-level assignment rule, pool Move/Keep, room-only returns.</summary>
    [TestFixture]
    public class Rel1009hTests
    {
        private const int AdminTop = 1;    // Administrative, top-level (store)
        private const int GradeTop = 2;    // Grade, top-level
        private const int SubDept = 3;     // SubDepartment under AdminTop
        private const int RoomA = 4;       // Room under SubDept
        private const int RoomB = 5;       // Room under SubDept
        private const int ClassC = 6;      // Class under GradeTop
        private const int SubDept2 = 7;    // SubDepartment under AdminTop

        // ---- item 4: rule ----

        [Test]
        public void AssignmentTarget_AllowsTopLevelTargets_AndMovesOutOfTopLevel()
        {
            Assert.IsTrue(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.Room, false, DepartmentKind.Administrative, true));
            Assert.IsTrue(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.SubDepartment, false, DepartmentKind.Grade, true));
            Assert.IsTrue(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.Administrative, true, DepartmentKind.Room, false));
            Assert.IsTrue(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.Administrative, true, DepartmentKind.SubDepartment, false));
            Assert.IsTrue(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.Room, false, DepartmentKind.Room, false));
            Assert.IsTrue(AssetMoveRules.IsAllowedAssignmentTarget(null, false, DepartmentKind.Class, false));
        }

        [Test]
        public void AssignmentTarget_StillRefusesNonTopLevelMovesOutsideTheMoveRules()
        {
            Assert.IsFalse(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.SubDepartment, false, DepartmentKind.SubDepartment, false));
            Assert.IsFalse(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.Room, false, DepartmentKind.Class, false));
            Assert.IsFalse(AssetMoveRules.IsAllowedAssignmentTarget(DepartmentKind.Class, false, DepartmentKind.SubDepartment, false));
        }

        [Test]
        public void PhysicalMoveRule_IsUnchanged()
        {
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Administrative, DepartmentKind.Room));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Room, DepartmentKind.Administrative));
            Assert.IsTrue(AssetMoveRules.IsAllowed(DepartmentKind.Room, DepartmentKind.SubDepartment));
        }

        [Test]
        public void PhysicalMoveRule_AllowsOnlyAdministrationDirectToRoom()
        {
            var admin = new Department { Name = " administration ", ParentDepartmentId = null, DepartmentKind = DepartmentKind.Administrative };
            var grade = new Department { Name = "Grade 2", ParentDepartmentId = null, DepartmentKind = DepartmentKind.Grade };
            var academics = new Department { Name = "Academics", ParentDepartmentId = null, DepartmentKind = DepartmentKind.Administrative };
            var underAdmin = new Department { Name = "Administration", ParentDepartmentId = 7, DepartmentKind = DepartmentKind.SubDepartment };
            Assert.IsTrue(AssetMoveRules.IsAdministrationRoot(admin));
            Assert.IsFalse(AssetMoveRules.IsAdministrationRoot(grade));
            Assert.IsFalse(AssetMoveRules.IsAdministrationRoot(academics));
            Assert.IsFalse(AssetMoveRules.IsAdministrationRoot(underAdmin));
            Assert.IsFalse(AssetMoveRules.IsAdministrationRoot(null));

            Assert.IsTrue(AssetMoveRules.IsAllowed(admin.DepartmentKind, AssetMoveRules.IsAdministrationRoot(admin), DepartmentKind.Room));
            Assert.IsFalse(AssetMoveRules.IsAllowed(grade.DepartmentKind, AssetMoveRules.IsAdministrationRoot(grade), DepartmentKind.Room));
            Assert.IsFalse(AssetMoveRules.IsAllowed(academics.DepartmentKind, AssetMoveRules.IsAdministrationRoot(academics), DepartmentKind.Room));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Administrative, true, DepartmentKind.SubDepartment));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Administrative, true, DepartmentKind.Class));
            Assert.IsFalse(AssetMoveRules.IsAllowed(DepartmentKind.Room, false, DepartmentKind.Class));
            Assert.IsTrue(AssetMoveRules.IsAllowed(DepartmentKind.Class, false, DepartmentKind.Room));
            Assert.IsTrue(AssetMoveRules.IsAllowed(DepartmentKind.SubDepartment, false, DepartmentKind.Room));
        }

        [Test]
        public void NeedsChoice_CoversCrossDepartmentPoolIntoSubOrTopDepartment_ButNotRooms()
        {
            Assert.IsTrue(CrossDepartmentDisposition.NeedsChoice(RoomA, SubDept, null, DepartmentKind.SubDepartment));
            Assert.IsTrue(CrossDepartmentDisposition.NeedsChoice(RoomA, AdminTop, null, DepartmentKind.Administrative));
            Assert.IsTrue(CrossDepartmentDisposition.NeedsChoice(RoomA, SubDept, "u1", DepartmentKind.SubDepartment));
            Assert.IsFalse(CrossDepartmentDisposition.NeedsChoice(SubDept, RoomA, null, DepartmentKind.Room));
            Assert.IsFalse(CrossDepartmentDisposition.NeedsChoice(GradeTop, ClassC, null, DepartmentKind.Class));
            Assert.IsFalse(CrossDepartmentDisposition.NeedsChoice(SubDept, SubDept, null, DepartmentKind.SubDepartment));
            Assert.IsFalse(CrossDepartmentDisposition.NeedsChoice(null, SubDept, null, DepartmentKind.SubDepartment));
        }

        [Test]
        public void PoolIntoTopLevelDepartment_WithoutChoice_KeepsAssetDepartment()
        {
            var uow = Seed();
            SeedAsset(uow, 100, RoomA);
            var service = TestServiceFactory.CreateAssignmentService(uow);

            service.Assign(PoolModel(100, AdminTop, null));

            var asset = uow.Repository<Asset>().GetById(100);
            Assert.AreEqual(RoomA, asset.DepartmentId);
            Assert.AreEqual(AssetStatus.Assigned, asset.CurrentStatus);
            Assert.IsNull(asset.CurrentCustodianId);
            var assignment = uow.Repository<AssetAssignment>().Query().Single(x => x.AssetId == 100);
            Assert.AreEqual(AdminTop, assignment.ToDepartmentId);
            Assert.AreEqual(AssignmentType.DepartmentPool, assignment.AssignmentType);
        }

        [Test]
        public void PoolIntoTopLevelDepartment_WithMove_IsNowAllowed()
        {
            var uow = Seed();
            SeedAsset(uow, 101, RoomA);
            var service = TestServiceFactory.CreateAssignmentService(uow);

            service.Assign(PoolModel(101, AdminTop, CrossDepartmentDisposition.Move));

            Assert.AreEqual(AdminTop, uow.Repository<Asset>().GetById(101).DepartmentId);
        }

        [Test]
        public void PoolIntoSubDepartment_Keep_DoesNotSilentlyMove_MoveFromRoomMoves()
        {
            var uow = Seed();
            SeedAsset(uow, 102, RoomA);
            SeedAsset(uow, 103, RoomB);
            var service = TestServiceFactory.CreateAssignmentService(uow);

            service.Assign(PoolModel(102, SubDept, CrossDepartmentDisposition.Keep));
            service.Assign(PoolModel(103, SubDept, CrossDepartmentDisposition.Move));

            Assert.AreEqual(RoomA, uow.Repository<Asset>().GetById(102).DepartmentId);
            Assert.AreEqual(SubDept, uow.Repository<Asset>().GetById(103).DepartmentId);
        }

        [Test]
        public void PoolFromTopLevelStoreIntoRoom_IsNowAllowed_AndMovesIntoRoom()
        {
            var uow = Seed();
            SeedAsset(uow, 104, AdminTop);
            var service = TestServiceFactory.CreateAssignmentService(uow);

            service.Assign(PoolModel(104, RoomA, null));

            Assert.AreEqual(RoomA, uow.Repository<Asset>().GetById(104).DepartmentId);
        }

        [Test]
        public void SubDepartmentToSubDepartment_Move_IsStillRefused()
        {
            var uow = Seed();
            SeedAsset(uow, 105, SubDept);
            var service = TestServiceFactory.CreateAssignmentService(uow);

            var ex = Assert.Throws<BusinessException>(() => service.Assign(PoolModel(105, SubDept2, CrossDepartmentDisposition.Move)));
            Assert.AreEqual(AssetMoveRules.AssignmentRefusedMessage, ex.Message);
        }

        // ---- item 2: batch ----

        [Test]
        public void BatchAssign_RowWithoutCustodian_BecomesPool_WhenDepartmentChosen()
        {
            var uow = Seed();
            SeedAsset(uow, 110, SubDept);
            SeedAsset(uow, 111, SubDept);
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "u-room", FirstName = "Room", LastName = "User", DepartmentId = RoomA, IsActive = true });
            var service = TestServiceFactory.CreateAssignmentService(uow, users);

            var result = service.BatchAssign(new BatchAssignRequestVm
            {
                HandedOverById = "admin-1",
                ToDepartmentId = RoomA,
                DepartmentDisposition = CrossDepartmentDisposition.Keep,
                Items = new List<BatchAssignItemVm>
                {
                    new BatchAssignItemVm { AssetId = 110, ToUserId = null },
                    new BatchAssignItemVm { AssetId = 111, ToUserId = "u-room" }
                }
            });

            Assert.AreEqual(2, result.ProcessedCount, string.Join(" | ", result.Rows.Select(r => r.Message)));
            var pooled = uow.Repository<Asset>().GetById(110);
            Assert.IsNull(pooled.CurrentCustodianId);
            Assert.AreEqual(AssetStatus.Assigned, pooled.CurrentStatus);
            Assert.AreEqual(RoomA, pooled.DepartmentId);
            Assert.AreEqual(AssignmentType.DepartmentPool, uow.Repository<AssetAssignment>().Query().Single(x => x.AssetId == 110).AssignmentType);
            Assert.AreEqual("u-room", uow.Repository<Asset>().GetById(111).CurrentCustodianId);
        }

        [Test]
        public void BatchAssign_RowWithoutCustodian_StillSkipped_WhenNoDepartment()
        {
            var uow = Seed();
            SeedAsset(uow, 112, SubDept);
            var service = TestServiceFactory.CreateAssignmentService(uow);

            var result = service.BatchAssign(new BatchAssignRequestVm
            {
                HandedOverById = "admin-1",
                Items = new List<BatchAssignItemVm> { new BatchAssignItemVm { AssetId = 112 } }
            });

            Assert.AreEqual(0, result.ProcessedCount);
            Assert.AreEqual(AssetStatus.InStore, uow.Repository<Asset>().GetById(112).CurrentStatus);
        }

        // ---- item 1: receive into store ----

        [Test]
        public void ReceiveIntoStore_FulfilsApprovedRequest_AndLeavesAssetInStore()
        {
            var uow = Seed();
            SeedAsset(uow, 120, SubDept);
            SeedRequest(uow, 50, AssetRequestStatus.Approved);
            var service = TestServiceFactory.CreateAssetRequestService(uow, new NoOpDepartmentScopeService());

            service.ReceiveIntoStore(50, 120, "approver-1", "received into store");

            var request = uow.Repository<AssetRequest>().GetById(50);
            Assert.AreEqual(AssetRequestStatus.Fulfilled, request.Status);
            Assert.AreEqual(120, request.FulfilledAssetId);
            Assert.AreEqual("approver-1", request.ReviewedById);
            var asset = uow.Repository<Asset>().GetById(120);
            Assert.AreEqual(AssetStatus.InStore, asset.CurrentStatus);
            Assert.IsNull(asset.CurrentCustodianId);
            Assert.AreEqual(SubDept, asset.DepartmentId);
            Assert.IsFalse(uow.Repository<AssetAssignment>().Query().Any(x => x.AssetId == 120));
        }

        [Test]
        public void ReceiveIntoStore_RejectsPendingRequest_AssignedAsset_AndTheRequester()
        {
            var uow = Seed();
            SeedAsset(uow, 121, SubDept);
            SeedAsset(uow, 122, SubDept);
            var assigned = uow.Repository<Asset>().GetById(122);
            assigned.CurrentStatus = AssetStatus.Assigned;
            assigned.CurrentCustodianId = "someone";
            SeedRequest(uow, 51, AssetRequestStatus.Pending);
            SeedRequest(uow, 52, AssetRequestStatus.Approved);
            var service = TestServiceFactory.CreateAssetRequestService(uow, new NoOpDepartmentScopeService());

            Assert.Throws<BusinessException>(() => service.ReceiveIntoStore(51, 121, "approver-1", null));
            Assert.Throws<BusinessException>(() => service.ReceiveIntoStore(52, 122, "approver-1", null));
            Assert.Throws<BusinessException>(() => service.ReceiveIntoStore(52, 121, "requester-1", null));
            Assert.AreEqual(AssetRequestStatus.Approved, uow.Repository<AssetRequest>().GetById(52).Status);
        }

        // ---- item 5: returns ----

        [Test]
        public void Return_RoomPoolWithoutCustodian_AllowsBlankReturnedBy()
        {
            var uow = Seed();
            SeedAsset(uow, 130, RoomA);
            var asset = uow.Repository<Asset>().GetById(130);
            asset.CurrentStatus = AssetStatus.Assigned;
            var service = TestServiceFactory.CreateReturnService(uow);

            service.ReturnAsset(new AssetReturnVm { AssetId = 130, ReturnCondition = AssetCondition.Good.ToString(), ReturnDate = DateTime.UtcNow });

            var record = uow.Repository<AssetReturn>().Query().Single(x => x.AssetId == 130);
            Assert.IsNull(record.ReturnedById);
            Assert.AreEqual(AssetStatus.Returned, uow.Repository<Asset>().GetById(130).CurrentStatus);
        }

        [Test]
        public void Return_RoomPoolWithoutCustodian_AcceptsAnyActiveUser_RejectsUnknown()
        {
            var uow = Seed();
            SeedAsset(uow, 131, RoomA);
            SeedAsset(uow, 132, RoomA);
            uow.Repository<Asset>().GetById(131).CurrentStatus = AssetStatus.Assigned;
            uow.Repository<Asset>().GetById(132).CurrentStatus = AssetStatus.Assigned;
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "other-dept-user", FirstName = "Other", LastName = "Dept", DepartmentId = GradeTop, IsActive = true });
            var service = TestServiceFactory.CreateReturnService(uow, users);

            service.ReturnAsset(new AssetReturnVm { AssetId = 131, ReturnedById = "other-dept-user", ReturnCondition = AssetCondition.Good.ToString(), ReturnDate = DateTime.UtcNow });
            Assert.AreEqual("other-dept-user", uow.Repository<AssetReturn>().Query().Single(x => x.AssetId == 131).ReturnedById);

            Assert.Throws<BusinessException>(() => service.ReturnAsset(new AssetReturnVm { AssetId = 132, ReturnedById = "ghost", ReturnCondition = AssetCondition.Good.ToString(), ReturnDate = DateTime.UtcNow }));
        }

        // ---- seed helpers ----

        private static FakeUnitOfWork Seed()
        {
            var uow = new FakeUnitOfWork();
            SeedDept(uow, AdminTop, "Admin", DepartmentKind.Administrative, null);
            SeedDept(uow, GradeTop, "Grade 1", DepartmentKind.Grade, null);
            SeedDept(uow, SubDept, "ICT", DepartmentKind.SubDepartment, AdminTop);
            SeedDept(uow, RoomA, "Room A", DepartmentKind.Room, SubDept);
            SeedDept(uow, RoomB, "Room B", DepartmentKind.Room, SubDept);
            SeedDept(uow, ClassC, "Class 1C", DepartmentKind.Class, GradeTop);
            SeedDept(uow, SubDept2, "Finance", DepartmentKind.SubDepartment, AdminTop);
            return uow;
        }

        private static void SeedDept(FakeUnitOfWork uow, int id, string name, DepartmentKind kind, int? parentId)
        {
            uow.Seed(new Department { Id = id, Name = name, Code = "D" + id, DepartmentKind = kind, ParentDepartmentId = parentId, IsActive = true });
        }

        private static void SeedAsset(FakeUnitOfWork uow, int id, int departmentId)
        {
            uow.Seed(new Asset
            {
                Id = id,
                AssetTag = "R9H-" + id,
                AssetName = "Laptop",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = departmentId,
                Currency = "USD",
                AcquisitionCost = 1000,
                Condition = AssetCondition.Good,
                CurrentStatus = AssetStatus.InStore,
                IsActive = true,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow,
                UsefulLifeMonths = 36,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static void SeedRequest(FakeUnitOfWork uow, int id, AssetRequestStatus status)
        {
            uow.Seed(new AssetRequest
            {
                Id = id,
                RequestedById = "requester-1",
                DepartmentId = SubDept,
                Justification = "rel1009h",
                Status = status,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
        }

        private static AssetAssignmentVm PoolModel(int assetId, int departmentId, string disposition)
        {
            return new AssetAssignmentVm
            {
                AssetId = assetId,
                ToDepartmentId = departmentId,
                AssignmentType = AssignmentType.DepartmentPool.ToString(),
                AssignedDate = DateTime.UtcNow,
                ConditionBeforeHandover = AssetCondition.Good.ToString(),
                HandedOverById = "admin-1",
                DepartmentDisposition = disposition
            };
        }
    }
}
