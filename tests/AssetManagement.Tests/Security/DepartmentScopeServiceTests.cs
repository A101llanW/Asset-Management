using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class DepartmentScopeServiceTests
    {
        [Test]
        public void ApplyAssetScope_FiltersToUserDepartment_WhenNotSuperAdmin()
        {
            var assets = new List<Asset>
            {
                new Asset { Id = 1, DepartmentId = 10, IsActive = true, AssetTag = "A1", CurrentStatus = AssetStatus.InStore },
                new Asset { Id = 2, DepartmentId = 20, IsActive = true, AssetTag = "A2", CurrentStatus = AssetStatus.InStore }
            };

            var unitOfWork = new Mock<IUnitOfWork>();
            var roleRepo = new Mock<IRepository<Role>>();
            roleRepo.Setup(x => x.GetById(5)).Returns(new Role { Id = 5, Name = "Staff", IsSystemRole = false });
            unitOfWork.Setup(x => x.Repository<Role>()).Returns(roleRepo.Object);

            var userService = new Mock<IUserService>();
            var currentUser = new Mock<ICurrentUserContext>();

            currentUser.Setup(x => x.UserId).Returns("user-1");
            userService.Setup(x => x.GetById("user-1")).Returns(new UserVm
            {
                Id = "user-1",
                DepartmentId = 10,
                RoleId = 5
            });

            var service = CreateDepartmentScopeService(unitOfWork.Object, currentUser.Object, userService.Object, isCompanyAdmin: false);
            var scoped = service.ApplyAssetScope(assets.AsQueryable()).ToList();

            Assert.AreEqual(1, scoped.Count);
            Assert.AreEqual(10, scoped[0].DepartmentId);
        }

        [Test]
        public void BypassesDepartmentScope_ReturnsTrue_ForSuperAdmin()
        {
            var unitOfWork = new Mock<IUnitOfWork>();
            var roleRepo = new Mock<IRepository<Role>>();
            roleRepo.Setup(x => x.GetById(1)).Returns(new Role { Id = 1, Name = "Super Admin", IsSystemRole = true });
            unitOfWork.Setup(x => x.Repository<Role>()).Returns(roleRepo.Object);

            var userService = new Mock<IUserService>();
            var currentUser = new Mock<ICurrentUserContext>();

            currentUser.Setup(x => x.UserId).Returns("admin");
            userService.Setup(x => x.GetById("admin")).Returns(new UserVm
            {
                Id = "admin",
                DepartmentId = 10,
                RoleId = 1
            });

            var service = CreateDepartmentScopeService(unitOfWork.Object, currentUser.Object, userService.Object, isCompanyAdmin: true);

            Assert.IsTrue(service.BypassesDepartmentScope);
            Assert.IsNull(service.ScopedDepartmentId);
        }

        private static DepartmentScopeService CreateDepartmentScopeService(
            IUnitOfWork unitOfWork,
            ICurrentUserContext currentUser,
            IUserService userService,
            bool isCompanyAdmin)
        {
            return new DepartmentScopeService(unitOfWork, currentUser, userService, new FakeOrganizationScopeService(companyAdmin: isCompanyAdmin));
        }
    }
}
