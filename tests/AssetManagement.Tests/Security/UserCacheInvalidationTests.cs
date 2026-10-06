using System.Collections.Generic;
using System.Web;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class UserCacheInvalidationTests
    {
        [Test]
        public void InvalidateUsers_RemovesEveryDepartmentListForThatOrganizationOnly()
        {
            var cache = new ReferenceDataCache(Mock.Of<ISqlConnectionFactory>());
            HttpRuntime.Cache.Insert("org:9101:users:all", new List<string>());
            HttpRuntime.Cache.Insert("org:9101:users:1594", new List<string>());
            HttpRuntime.Cache.Insert("org:91011:users:all", new List<string>());
            HttpRuntime.Cache.Insert("org:9101:roles", new List<string>());

            cache.InvalidateUsers(9101);

            Assert.IsNull(HttpRuntime.Cache["org:9101:users:all"]);
            Assert.IsNull(HttpRuntime.Cache["org:9101:users:1594"]);
            Assert.IsNotNull(HttpRuntime.Cache["org:91011:users:all"], "Other organization (prefix collision) must be untouched.");
            Assert.IsNotNull(HttpRuntime.Cache["org:9101:roles"], "Non-user keys must be untouched.");

            HttpRuntime.Cache.Remove("org:91011:users:all");
            HttpRuntime.Cache.Remove("org:9101:roles");
        }

        [Test]
        public void InvalidateDepartments_AlsoDropsUserDropdownLists()
        {
            var cache = new ReferenceDataCache(Mock.Of<ISqlConnectionFactory>());
            HttpRuntime.Cache.Insert("org:9102:users:all", new List<string>());
            HttpRuntime.Cache.Insert("org:9102:depts:active", new List<string>());

            cache.InvalidateDepartments(9102);

            Assert.IsNull(HttpRuntime.Cache["org:9102:users:all"]);
            Assert.IsNull(HttpRuntime.Cache["org:9102:depts:active"]);
        }

        [Test]
        public void AssignDepartment_InvalidatesUserDropdownCache()
        {
            var user = new ApplicationUser { Id = "target", IsActive = true, OrganizationId = 23 };
            var department = new Department { Id = 1594, Name = "Boardroom 1", OrganizationId = 23, IsActive = true };

            var userWriter = new Mock<IEntityWriter<ApplicationUser>>();
            userWriter.Setup(x => x.GetById("target")).Returns(user);
            var departmentWriter = new Mock<IEntityWriter<Department>>();
            departmentWriter.Setup(x => x.GetById(1594)).Returns(department);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.Writer<ApplicationUser>()).Returns(userWriter.Object);
            unitOfWork.Setup(x => x.Writer<Department>()).Returns(departmentWriter.Object);

            var referenceDataCache = new Mock<IReferenceDataCache>();
            var service = new UserService(
                unitOfWork.Object,
                Mock.Of<IAuthorizationService>(),
                Mock.Of<IOrganizationScopeService>(),
                new FakeCurrentUserContext("admin-1"),
                Mock.Of<IAuditWriter>(),
                Mock.Of<IUserAccountQueryRepository>(),
                referenceDataCache.Object);

            service.AssignDepartment("target", 1594);

            referenceDataCache.Verify(x => x.InvalidateUsers(23), Times.Once);
        }

        [Test]
        public void AssignRole_InvalidatesUserDropdownCache()
        {
            var user = new ApplicationUser { Id = "target", RoleId = 2, IsActive = true, OrganizationId = 23 };
            var role = new Role { Id = 145, Name = "Staff", IsSystemRole = false, IsActive = true, OrganizationId = 23 };

            var userWriter = new Mock<IEntityWriter<ApplicationUser>>();
            userWriter.Setup(x => x.GetById("target")).Returns(user);
            var roleWriter = new Mock<IEntityWriter<Role>>();
            roleWriter.Setup(x => x.GetById(145)).Returns(role);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.Writer<ApplicationUser>()).Returns(userWriter.Object);
            unitOfWork.Setup(x => x.Writer<Role>()).Returns(roleWriter.Object);

            var orgScope = new Mock<IOrganizationScopeService>();
            orgScope.Setup(x => x.IsCompanyAdmin()).Returns(true);

            var referenceDataCache = new Mock<IReferenceDataCache>();
            var service = new UserService(
                unitOfWork.Object,
                Mock.Of<IAuthorizationService>(),
                orgScope.Object,
                new FakeCurrentUserContext("admin-1"),
                Mock.Of<IAuditWriter>(),
                Mock.Of<IUserAccountQueryRepository>(),
                referenceDataCache.Object);

            service.AssignRole("target", 145);

            referenceDataCache.Verify(x => x.InvalidateUsers(23), Times.Once);
        }
    }
}
