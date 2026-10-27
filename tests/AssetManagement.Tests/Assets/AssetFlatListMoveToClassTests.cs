using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    /// <summary>
    /// Allan, 2026-10-27: "Move to classroom" in the FLAT asset list (per row and bulk), with the same rules as the
    /// grouped view (0d5ca73): classroom to classroom and classroom to room allowed, room or sub-department to room
    /// only, no inactive targets.
    /// </summary>
    [TestFixture]
    public class AssetFlatListMoveToClassTests
    {
        private const string ActorUserId = "user-flat-move";

        [Test]
        public void BulkMoveToClass_MovesClassroomAssets_AndSkipsRoomSubDepartmentAndAssignedOnes()
        {
            var unitOfWork = Seed();
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var result = service.RelocateSelectedToClassDepartment(new[] { 1, 2, 3, 4, 5, 1 }, 2, ActorUserId);

            Assert.AreEqual(2, result.ProcessedCount, string.Join(" | ", result.Messages));
            Assert.AreEqual(3, result.SkippedCount);
            Assert.AreEqual(2, unitOfWork.Repository<Asset>().GetById(1).DepartmentId, "classroom -> classroom");
            Assert.AreEqual(2, unitOfWork.Repository<Asset>().GetById(5).DepartmentId, "company custody placement");
            Assert.AreEqual(10, unitOfWork.Repository<Asset>().GetById(2).DepartmentId, "room stays (room -> room only)");
            Assert.AreEqual(11, unitOfWork.Repository<Asset>().GetById(3).DepartmentId, "sub-department stays (-> room only)");
            Assert.AreEqual(1, unitOfWork.Repository<Asset>().GetById(4).DepartmentId, "assigned asset stays");
            Assert.IsTrue(result.Messages.Any(m => m.StartsWith("ROOM-1: ") && m.Contains(AssetMoveRules.RefusedMessage)));
            Assert.IsTrue(result.Messages.Any(m => m.StartsWith("SUB-1: ") && m.Contains(AssetMoveRules.RefusedMessage)));
            Assert.IsTrue(result.Messages.Any(m => m.StartsWith("CLS-ASSIGNED: ") && m.Contains("custodian")));

            var events = unitOfWork.Repository<AssetCustodyEvent>().GetAll().ToList();
            Assert.AreEqual(2, events.Count);
            Assert.IsTrue(events.Any(e => e.AssetId == 1 && e.FromDepartmentId == 1 && e.ToDepartmentId == 2));
        }

        [Test]
        public void BulkMoveToClass_InactiveClassroom_IsRefusedForEveryAsset()
        {
            var unitOfWork = Seed();
            unitOfWork.Repository<Department>().GetById(2).IsActive = false;
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var result = service.RelocateSelectedToClassDepartment(new[] { 1, 5 }, 2, ActorUserId);

            Assert.AreEqual(0, result.ProcessedCount);
            Assert.AreEqual(2, result.SkippedCount);
            Assert.IsTrue(result.Messages.All(m => m.Contains("inactive")));
            Assert.AreEqual(1, unitOfWork.Repository<Asset>().GetById(1).DepartmentId);
            Assert.IsNull(unitOfWork.Repository<Asset>().GetById(5).DepartmentId);
        }

        [Test]
        public void BulkMoveToClass_RoomTarget_IsRefused()
        {
            var unitOfWork = Seed();
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            var result = service.RelocateSelectedToClassDepartment(new[] { 1 }, 10, ActorUserId);

            Assert.AreEqual(0, result.ProcessedCount);
            StringAssert.Contains("must be a classroom", result.Messages.Single());
        }

        [Test]
        public void BulkMoveToClass_NoSelectionOrNoTarget_Throws()
        {
            var unitOfWork = Seed();
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new FixedPermissionAuthorizationService("Assets.Edit"));

            Assert.Throws<BusinessException>(() => service.RelocateSelectedToClassDepartment(new int[0], 2, ActorUserId));
            Assert.Throws<BusinessException>(() => service.RelocateSelectedToClassDepartment(null, 2, ActorUserId));
            Assert.Throws<BusinessException>(() => service.RelocateSelectedToClassDepartment(new[] { 1 }, 0, ActorUserId));
        }

        [Test]
        public void BulkMoveToClass_WithoutEditOrTransfer_MovesNothing()
        {
            var unitOfWork = Seed();
            var service = TestServiceFactory.CreateAssetService(
                unitOfWork,
                authorization: new DenyAllAuthorizationService());

            var result = service.RelocateSelectedToClassDepartment(new[] { 1 }, 2, ActorUserId);

            Assert.AreEqual(0, result.ProcessedCount);
            StringAssert.Contains("permission", result.Messages.Single());
            Assert.AreEqual(1, unitOfWork.Repository<Asset>().GetById(1).DepartmentId);
        }

        [Test]
        public void FlatList_HasPerRowAndBulkMoveToClassroom_WiredToTheClassroomMove()
        {
            var root = RepositoryRoot();
            var web = Path.Combine(root, "src", "AssetManagement.Web");
            var index = File.ReadAllText(Path.Combine(web, "Views", "Assets", "Index.cshtml"));
            var toolbar = File.ReadAllText(Path.Combine(web, "Views", "Shared", "_ListBulkToolbar.cshtml"));
            var script = File.ReadAllText(Path.Combine(web, "Scripts", "app", "list-bulk.js"));
            var controller = File.ReadAllText(Path.Combine(web, "Controllers", "AssetsController.cs"));

            // Per row: the button only for classroom (or company custody) assets, using the same move-source list as the grouped view.
            StringAssert.Contains("data-am-relocate-class-open", index);
            Assert.IsTrue(Regex.IsMatch(index, @"canMoveToClass\s*&&\s*\(!item\.DepartmentId\.HasValue \|\| moveToClassSourceIds\.Contains\(item\.DepartmentId\.Value\)\)"));
            // The single-asset classroom modal is rendered outside the grouped branch and returns to the current view.
            StringAssert.Contains("@if (canMoveToClass)", index);
            Assert.AreEqual(1, Regex.Matches(index, @"@Html\.Hidden\(""view"", ""grouped""\)").Count, "only the group-to-classroom modal is grouped-only");

            // Bulk: option + classroom select (flat list only), shown by list-bulk.js, routed to the classroom move.
            StringAssert.Contains("<option value=\"classroom\">Move to classroom</option>", toolbar);
            StringAssert.Contains("TargetClassDepartmentId", toolbar);
            StringAssert.Contains("var isFlat = !isGrouped && !isRoom;", toolbar);
            StringAssert.Contains("data-am-bulk-class", script);
            StringAssert.Contains("RelocateSelectedToClassDepartment(", controller);
            Assert.IsFalse(Regex.IsMatch(index + toolbar + script, @"9100|9101|BrowserPrint|window\.print", RegexOptions.IgnoreCase));
        }

        private static FakeUnitOfWork Seed()
        {
            var unitOfWork = new FakeUnitOfWork();
            var now = DateTime.UtcNow;
            unitOfWork.Seed(new Department { Id = 1, Name = "Class A", Code = "CLS-A", DepartmentKind = DepartmentKind.Class, CreatedAt = now, IsActive = true });
            unitOfWork.Seed(new Department { Id = 2, Name = "Class B", Code = "CLS-B", DepartmentKind = DepartmentKind.Class, CreatedAt = now, IsActive = true });
            unitOfWork.Seed(new Department { Id = 10, Name = "Room 1", Code = "RM1", DepartmentKind = DepartmentKind.Room, CreatedAt = now, IsActive = true });
            unitOfWork.Seed(new Department { Id = 11, Name = "Homerooms", Code = "SUB", DepartmentKind = DepartmentKind.SubDepartment, CreatedAt = now, IsActive = true });

            unitOfWork.Seed(NewAsset(1, "CLS-1", 1, AssetStatus.InStore, null, now));
            unitOfWork.Seed(NewAsset(2, "ROOM-1", 10, AssetStatus.InStore, null, now));
            unitOfWork.Seed(NewAsset(3, "SUB-1", 11, AssetStatus.InStore, null, now));
            unitOfWork.Seed(NewAsset(4, "CLS-ASSIGNED", 1, AssetStatus.Assigned, "custodian-1", now));
            unitOfWork.Seed(NewAsset(5, "CUSTODY-1", null, AssetStatus.InStore, null, now));
            return unitOfWork;
        }

        private static Asset NewAsset(int id, string tag, int? departmentId, AssetStatus status, string custodianId, DateTime now)
        {
            return new Asset
            {
                Id = id,
                AssetTag = tag,
                AssetName = "Chair",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = departmentId,
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
            };
        }

        private static string RepositoryRoot()
        {
            var fromEnvironment = Environment.GetEnvironmentVariable("CODEXASSET_ROOT");
            if (!string.IsNullOrWhiteSpace(fromEnvironment) && Directory.Exists(Path.Combine(fromEnvironment, "src", "AssetManagement.Application")))
            {
                return fromEnvironment;
            }

            var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "src", "AssetManagement.Application")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            Assert.Inconclusive("Repository root not found.");
            return null;
        }
    }
}
