using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Approvals
{
    /// <summary>
    /// Allan 2026-11-02 correction 3 (department model): a department head heads one department (Users.DepartmentId is a
    /// single value) and a department can have several heads (every active "Department Head" holder whose department is
    /// the unit). No schema change is needed: these tests are the evidence, alongside StageApproverScopeTests (rel1009e).
    /// </summary>
    [TestFixture]
    public class DepartmentHeadModelTests
    {
        private const int Hod = 63;
        private const int Staff = 64;

        private static FakeUnitOfWork Seed()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department { Id = 1456, Name = "Academics", DepartmentKind = DepartmentKind.Administrative, IsActive = true, IsRequisitionTarget = true, OrganizationId = 13 });
            uow.Seed(new Department { Id = 265, Name = "Library", ParentDepartmentId = 1456, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true, OrganizationId = 13 });
            uow.Seed(new Department { Id = 247, Name = "Finance", DepartmentKind = DepartmentKind.Administrative, IsActive = true, IsRequisitionTarget = true, OrganizationId = 13 });
            uow.Seed(new Role { Id = Hod, Name = "Department Head", IsActive = true, OrganizationId = 13 });
            uow.Seed(new Role { Id = Staff, Name = "Staff", IsActive = true, OrganizationId = 13 });
            return uow;
        }

        private static FakeUserService Users()
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "head-a", RoleId = Hod, DepartmentId = 1456, IsActive = true });
            users.Seed(new UserVm { Id = "head-b", RoleId = Hod, DepartmentId = 1456, IsActive = true });
            users.Seed(new UserVm { Id = "head-c", RoleId = Hod, DepartmentId = 1456, IsActive = true });
            users.Seed(new UserVm { Id = "head-finance", RoleId = Hod, DepartmentId = 247, IsActive = true });
            users.Seed(new UserVm { Id = "teacher", RoleId = Staff, DepartmentId = 1456, IsActive = true });
            return users;
        }

        [Test]
        public void ThreeHeadsOfOneDepartment_AreAllApprovers_OtherDepartmentHeadIsNot()
        {
            var scope = StageApproverScopeResolver.ResolveFor(Seed(), Users(), Hod, null, 1456);
            Assert.AreEqual(StageApproverScopeKind.Department, scope.Kind);
            CollectionAssert.AreEquivalent(new[] { "head-a", "head-b", "head-c" }, scope.UserIds);
            Assert.IsFalse(scope.Contains("head-finance"));
            Assert.IsFalse(scope.Contains("teacher"), "role-based: department members without the role are not heads");
        }

        [Test]
        public void RoomBelowTheDepartment_RoutesToAllItsHeads()
        {
            var scope = StageApproverScopeResolver.ResolveFor(Seed(), Users(), Hod, null, 265);
            Assert.AreEqual(StageApproverScopeKind.AncestorDepartment, scope.Kind);
            Assert.AreEqual(1456, scope.ResolvedDepartmentId);
            CollectionAssert.AreEquivalent(new[] { "head-a", "head-b", "head-c" }, scope.UserIds);
        }

        [Test]
        public void HeadsOneDepartment_UserDepartmentIsASingleValue()
        {
            var property = typeof(UserVm).GetProperty("DepartmentId");
            Assert.IsNotNull(property);
            Assert.AreEqual(typeof(int?), property.PropertyType, "one department per user: a head heads exactly one department");
            var financeOnly = StageApproverScopeResolver.ResolveFor(Seed(), Users(), Hod, null, 247);
            CollectionAssert.AreEqual(new[] { "head-finance" }, financeOnly.UserIds.ToList());
        }
    }
}
