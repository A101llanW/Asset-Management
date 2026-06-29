using System.Collections.Generic;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class RoleEscalationTests
    {
        [Test]
        public void AssignRole_BlocksRoleFromAnotherOrganization()
        {
            var user = new ApplicationUser { Id = "target", RoleId = 2, IsActive = true, OrganizationId = 2 };
            var role = new Role { Id = 9, Name = "Department Head", IsSystemRole = false, IsActive = true, OrganizationId = 1 };

            var userWriter = new Mock<IEntityWriter<ApplicationUser>>();
            userWriter.Setup(x => x.GetById("target")).Returns(user);
            var roleWriter = new Mock<IEntityWriter<Role>>();
            roleWriter.Setup(x => x.GetById(9)).Returns(role);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.Writer<ApplicationUser>()).Returns(userWriter.Object);
            unitOfWork.Setup(x => x.Writer<Role>()).Returns(roleWriter.Object);

            var orgScope = new Mock<IOrganizationScopeService>();
            orgScope.Setup(x => x.IsCompanyAdmin()).Returns(true);

            var service = new UserService(
                unitOfWork.Object,
                Mock.Of<IAuthorizationService>(),
                orgScope.Object,
                new FakeCurrentUserContext("admin-1"),
                Mock.Of<IAuditWriter>(),
                Mock.Of<IUserAccountQueryRepository>(),
                Mock.Of<IReferenceDataCache>());

            Assert.Throws<BusinessException>(() => service.AssignRole("target", 9));
        }

        [Test]
        public void AssignRole_BlocksSystemRole_WhenActorIsNotCompanyAdminOrPlatformAdmin()
        {
            var user = new ApplicationUser { Id = "target", RoleId = 2, IsActive = true, OrganizationId = 1 };
            var role = new Role { Id = 1, Name = "Company Admin", IsSystemRole = true, IsActive = true };

            var userWriter = new Mock<IEntityWriter<ApplicationUser>>();
            userWriter.Setup(x => x.GetById("target")).Returns(user);
            var roleWriter = new Mock<IEntityWriter<Role>>();
            roleWriter.Setup(x => x.GetById(1)).Returns(role);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.Writer<ApplicationUser>()).Returns(userWriter.Object);
            unitOfWork.Setup(x => x.Writer<Role>()).Returns(roleWriter.Object);

            var orgScope = new Mock<IOrganizationScopeService>();
            orgScope.Setup(x => x.IsCompanyAdmin()).Returns(false);
            orgScope.Setup(x => x.IsActualPlatformAdmin()).Returns(false);

            var service = new UserService(
                unitOfWork.Object,
                Mock.Of<IAuthorizationService>(),
                orgScope.Object,
                new FakeCurrentUserContext("manager-1"),
                Mock.Of<IAuditWriter>(),
                Mock.Of<IUserAccountQueryRepository>(),
                Mock.Of<IReferenceDataCache>());

            Assert.Throws<BusinessException>(() => service.AssignRole("target", 1));
        }

        [Test]
        public void AssignRole_AllowsSystemRole_WhenActorIsPlatformAdmin()
        {
            var user = new ApplicationUser { Id = "target", RoleId = 2, IsActive = true, OrganizationId = 1 };
            var role = new Role { Id = 1, Name = "Company Admin", IsSystemRole = true, IsActive = true };

            var userWriter = new Mock<IEntityWriter<ApplicationUser>>();
            userWriter.Setup(x => x.GetById("target")).Returns(user);
            var roleWriter = new Mock<IEntityWriter<Role>>();
            roleWriter.Setup(x => x.GetById(1)).Returns(role);

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.Writer<ApplicationUser>()).Returns(userWriter.Object);
            unitOfWork.Setup(x => x.Writer<Role>()).Returns(roleWriter.Object);
            unitOfWork.Setup(x => x.SaveChanges());

            var orgScope = new Mock<IOrganizationScopeService>();
            orgScope.Setup(x => x.IsCompanyAdmin()).Returns(false);
            orgScope.Setup(x => x.IsActualPlatformAdmin()).Returns(true);

            var service = new UserService(
                unitOfWork.Object,
                Mock.Of<IAuthorizationService>(),
                orgScope.Object,
                new FakeCurrentUserContext("platform-admin-1"),
                Mock.Of<IAuditWriter>(),
                Mock.Of<IUserAccountQueryRepository>(),
                Mock.Of<IReferenceDataCache>());

            Assert.DoesNotThrow(() => service.AssignRole("target", 1));
            userWriter.Verify(x => x.Update(It.Is<ApplicationUser>(u => u.RoleId == 1)), Times.Once);
        }

        [Test]
        public void AssignRole_BlocksPermissionCeilingViolation()
        {
            var user = new ApplicationUser { Id = "target", RoleId = 2, IsActive = true, OrganizationId = 1 };
            var role = new Role { Id = 3, Name = "Elevated", IsSystemRole = false, IsActive = true };

            var userWriter = new Mock<IEntityWriter<ApplicationUser>>();
            userWriter.Setup(x => x.GetById("target")).Returns(user);
            var roleWriter = new Mock<IEntityWriter<Role>>();
            roleWriter.Setup(x => x.GetById(3)).Returns(role);

            var rolePermissionRepo = new Mock<IRepository<RolePermission>>();
            rolePermissionRepo.Setup(x => x.Find(It.IsAny<System.Linq.Expressions.Expression<System.Func<RolePermission, bool>>>()))
                .Returns(new List<RolePermission> { new RolePermission { RoleId = 3, PermissionId = 99 } });

            var permissionRepo = new Mock<IRepository<Permission>>();
            permissionRepo.Setup(x => x.Find(It.IsAny<System.Linq.Expressions.Expression<System.Func<Permission, bool>>>()))
                .Returns(new List<Permission> { new Permission { Id = 99, Code = "Users.Edit", IsActive = true } });

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.Writer<ApplicationUser>()).Returns(userWriter.Object);
            unitOfWork.Setup(x => x.Writer<Role>()).Returns(roleWriter.Object);
            unitOfWork.Setup(x => x.Repository<RolePermission>()).Returns(rolePermissionRepo.Object);
            unitOfWork.Setup(x => x.Repository<Permission>()).Returns(permissionRepo.Object);

            var orgScope = new Mock<IOrganizationScopeService>();
            orgScope.Setup(x => x.IsCompanyAdmin()).Returns(false);
            orgScope.Setup(x => x.IsActualPlatformAdmin()).Returns(false);

            var auth = new Mock<IAuthorizationService>();
            auth.Setup(x => x.HasPermission("manager-1", "Users.Edit")).Returns(false);

            var service = new UserService(
                unitOfWork.Object,
                auth.Object,
                orgScope.Object,
                new FakeCurrentUserContext("manager-1"),
                Mock.Of<IAuditWriter>(),
                Mock.Of<IUserAccountQueryRepository>(),
                Mock.Of<IReferenceDataCache>());

            var ex = Assert.Throws<BusinessException>(() => service.AssignRole("target", 3));
            StringAssert.Contains("Users.Edit", ex.Message);
        }
    }
}
