using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Security;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// 2026-10-27 (Allan): a user with no department gets organization-wide scope (whole tenant, never another
    /// tenant) instead of empty lists, 403s or crashes. Users in a department keep their department scope.
    /// </summary>
    [TestFixture]
    public class NoDepartmentOrgScopeTests
    {
        private static List<Asset> Assets()
        {
            return new List<Asset>
            {
                new Asset { Id = 1, DepartmentId = 10, IsActive = true, AssetTag = "A1", CurrentStatus = AssetStatus.InStore },
                new Asset { Id = 2, DepartmentId = 20, IsActive = true, AssetTag = "A2", CurrentStatus = AssetStatus.InStore },
                new Asset { Id = 3, DepartmentId = null, IsActive = true, AssetTag = "A3", CurrentStatus = AssetStatus.InStore }
            };
        }

        private static List<Department> Departments()
        {
            return new List<Department>
            {
                new Department { Id = 10, Name = "Facilities", IsActive = true, DepartmentKind = DepartmentKind.Administrative },
                new Department { Id = 20, Name = "IT", IsActive = true, DepartmentKind = DepartmentKind.Administrative }
            };
        }

        private static DepartmentScopeService Build(string userId, UserVm profile, int organizationId = 1)
        {
            var unitOfWork = new Mock<IUnitOfWork>();
            var departmentRepo = new Mock<IRepository<Department>>();
            departmentRepo.Setup(x => x.Query()).Returns(Departments().AsQueryable());
            departmentRepo.Setup(x => x.GetById(It.IsAny<object>())).Returns((Department)null);
            unitOfWork.Setup(x => x.Repository<Department>()).Returns(departmentRepo.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(x => x.UserId).Returns(userId);
            var userService = new Mock<IUserService>();
            if (!string.IsNullOrWhiteSpace(userId))
            {
                userService.Setup(x => x.GetById(userId)).Returns(profile);
            }

            // No Assets.ViewAll, no Assets.Transfer: only the department rule decides the scope.
            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(x => x.HasPermission(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

            return new DepartmentScopeService(
                unitOfWork.Object,
                currentUser.Object,
                userService.Object,
                new FakeOrganizationScopeService(organizationId: organizationId, companyAdmin: false),
                authorization.Object);
        }

        private static UserVm Profile(string id, int? departmentId)
        {
            return new UserVm { Id = id, DepartmentId = departmentId, RoleId = null, IsActive = true };
        }

        [TestCase(null, true)]
        [TestCase(0, true)]
        [TestCase(-1, true)]
        [TestCase(5, false)]
        public void Rules_HasNoDepartment(int? departmentId, bool expected)
        {
            Assert.AreEqual(expected, DepartmentScopeRules.HasNoDepartment(departmentId));
        }

        [Test]
        public void Rules_IsUnassignedMember_NeedsAProfile()
        {
            Assert.IsFalse(DepartmentScopeRules.IsUnassignedMember(null));
            Assert.IsTrue(DepartmentScopeRules.IsUnassignedMember(Profile("u", null)));
            Assert.IsFalse(DepartmentScopeRules.IsUnassignedMember(Profile("u", 10)));
        }

        [Test]
        public void NoDepartmentUser_IsOrganizationWide()
        {
            var service = Build("fm-nodept", Profile("fm-nodept", null));

            Assert.IsTrue(service.IsUnassignedOrganizationWide);
            Assert.IsTrue(service.BypassesDepartmentScope);
            Assert.IsNull(service.ScopedDepartmentId);
            Assert.IsFalse(service.IncludesClassDepartmentAssets);
            Assert.AreEqual(3, service.ApplyAssetScope(Assets().AsQueryable()).Count());
            Assert.AreEqual(2, service.ApplyDepartmentScope(Departments().AsQueryable()).Count());
            Assert.AreEqual(2, service.CountVisibleDepartments());
        }

        [Test]
        public void NoDepartmentUser_EnsureChecksDoNotThrow()
        {
            var service = Build("fm-nodept", Profile("fm-nodept", null));

            Assert.DoesNotThrow(() => service.EnsureCanAccessAsset(Assets()[1]));
            Assert.DoesNotThrow(() => service.EnsureCanAccessDepartment(Departments()[1]));
            Assert.DoesNotThrow(() => service.EnsureCanAccessDepartmentId(20));
        }

        [Test]
        public void DepartmentZero_IsTreatedAsNoDepartment()
        {
            var service = Build("fm-zero", Profile("fm-zero", 0));

            Assert.IsTrue(service.BypassesDepartmentScope);
            Assert.AreEqual(3, service.ApplyAssetScope(Assets().AsQueryable()).Count());
        }

        [Test]
        public void DepartmentUser_KeepsDepartmentScope()
        {
            var service = Build("staff-10", Profile("staff-10", 10));

            Assert.IsFalse(service.IsUnassignedOrganizationWide);
            Assert.IsFalse(service.BypassesDepartmentScope);
            Assert.AreEqual(10, service.ScopedDepartmentId);
            var assets = service.ApplyAssetScope(Assets().AsQueryable()).ToList();
            Assert.AreEqual(1, assets.Count);
            Assert.AreEqual(10, assets[0].DepartmentId);
            Assert.AreEqual(1, service.ApplyDepartmentScope(Departments().AsQueryable()).Count());
            var ex = Assert.Throws<BusinessException>(() => service.EnsureCanAccessDepartmentId(20));
            StringAssert.Contains("outside your scope", ex.Message);
        }

        [Test]
        public void UnknownUser_StaysFailClosed()
        {
            // No profile in this organization (another tenant's user, deleted user): never widened.
            var service = Build("other-tenant-user", null);

            Assert.IsFalse(service.IsUnassignedOrganizationWide);
            Assert.IsFalse(service.BypassesDepartmentScope);
            Assert.AreEqual(0, service.ApplyAssetScope(Assets().AsQueryable()).Count());
            Assert.Throws<BusinessException>(() => service.EnsureCanAccessAsset(Assets()[0]));
        }

        [Test]
        public void Anonymous_StaysFailClosed()
        {
            var service = Build(null, null);

            Assert.IsFalse(service.BypassesDepartmentScope);
            Assert.AreEqual(0, service.ApplyAssetScope(Assets().AsQueryable()).Count());
        }

        [Test]
        public void TenantQueryScope_NoDepartmentUser_IsOrgWide_ButKeepsOrganization()
        {
            var org = new FakeOrganizationScopeService(organizationId: 13, companyAdmin: false);
            var scope = TenantQueryScope.Resolve(org, Build("fm-nodept", Profile("fm-nodept", null), 13));

            Assert.AreEqual(13, scope.OrganizationId);
            Assert.IsTrue(scope.BypassesDepartmentScope);
            Assert.IsFalse(scope.DenyDepartmentScope);
            Assert.IsNull(scope.DepartmentId);
            Assert.IsFalse(scope.IncludeClassDepartmentAssets);
        }

        [Test]
        public void TenantQueryScope_DepartmentUser_Unchanged()
        {
            var org = new FakeOrganizationScopeService(organizationId: 13, companyAdmin: false);
            var scope = TenantQueryScope.Resolve(org, Build("staff-10", Profile("staff-10", 10), 13));

            Assert.AreEqual(13, scope.OrganizationId);
            Assert.IsFalse(scope.BypassesDepartmentScope);
            Assert.IsFalse(scope.DenyDepartmentScope);
            Assert.AreEqual(10, scope.DepartmentId);
        }

        [Test]
        public void PrintLog_NoDepartmentUser_SeesEveryAssetInOrganization()
        {
            var service = Build("fm-nodept", Profile("fm-nodept", null));

            Assert.IsTrue(LabelPrintLogService.IsAssetInScope(service, 20, false));
            Assert.IsTrue(LabelPrintLogService.IsAssetInScope(service, null, false));
        }

        [Test]
        public void PrintLog_DepartmentUser_OnlyOwnDepartment()
        {
            var service = Build("staff-10", Profile("staff-10", 10));

            Assert.IsTrue(LabelPrintLogService.IsAssetInScope(service, 10, false));
            Assert.IsFalse(LabelPrintLogService.IsAssetInScope(service, 20, false));
        }
    }
}
