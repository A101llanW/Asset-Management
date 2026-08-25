using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
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
        public void ApplyAssetScope_IncludesClassDepartments_WhenUserHasAssetsTransfer()
        {
            var assets = new List<Asset>
            {
                new Asset { Id = 1, DepartmentId = 10, IsActive = true, AssetTag = "FAC-001", CurrentStatus = AssetStatus.InStore },
                new Asset { Id = 2, DepartmentId = 20, IsActive = true, AssetTag = "IT-001", CurrentStatus = AssetStatus.InStore },
                new Asset { Id = 3, DepartmentId = 30, IsActive = true, AssetTag = "CLS-001", CurrentStatus = AssetStatus.InStore }
            };
            var departments = new List<Department>
            {
                new Department { Id = 10, Name = "Facilities", IsActive = true, DepartmentKind = DepartmentKind.Administrative },
                new Department { Id = 20, Name = "IT", IsActive = true, DepartmentKind = DepartmentKind.Administrative },
                new Department { Id = 30, Name = "Grade 3A", IsActive = true, DepartmentKind = DepartmentKind.Class }
            };

            var unitOfWork = new Mock<IUnitOfWork>();
            var roleRepo = new Mock<IRepository<Role>>();
            roleRepo.Setup(x => x.GetById(5)).Returns(new Role { Id = 5, Name = "Facilities Manager", IsSystemRole = false });

            var departmentRepo = new Mock<IRepository<Department>>();
            departmentRepo.Setup(x => x.GetById(30)).Returns(departments[2]);
            departmentRepo.Setup(x => x.Query()).Returns(departments.AsQueryable());

            unitOfWork.Setup(x => x.Repository<Role>()).Returns(roleRepo.Object);
            unitOfWork.Setup(x => x.Repository<Department>()).Returns(departmentRepo.Object);

            var userService = new Mock<IUserService>();
            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(x => x.UserId).Returns("facilities-user");
            userService.Setup(x => x.GetById("facilities-user")).Returns(new UserVm
            {
                Id = "facilities-user",
                DepartmentId = 10,
                RoleId = 5
            });

            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(x => x.HasPermission("facilities-user", "Assets.Transfer")).Returns(true);

            var service = CreateDepartmentScopeService(
                unitOfWork.Object,
                currentUser.Object,
                userService.Object,
                false,
                authorization.Object);

            Assert.IsTrue(service.IncludesClassDepartmentAssets);

            var scoped = service.ApplyAssetScope(assets.AsQueryable()).ToList();
            Assert.AreEqual(2, scoped.Count);
            CollectionAssert.AreEquivalent(new[] { 1, 3 }, scoped.Select(x => x.Id).ToArray());
        }

        [Test]
        public void EnsureCanAccessAsset_AllowsClassDepartmentAsset_ForTransferUser()
        {
            var asset = new Asset { Id = 3, DepartmentId = 30, IsActive = true, AssetTag = "CLS-001", CurrentStatus = AssetStatus.InStore };
            var classDepartment = new Department { Id = 30, Name = "Grade 3A", IsActive = true, DepartmentKind = DepartmentKind.Class };

            var unitOfWork = new Mock<IUnitOfWork>();
            var roleRepo = new Mock<IRepository<Role>>();
            roleRepo.Setup(x => x.GetById(5)).Returns(new Role { Id = 5, Name = "Facilities Manager", IsSystemRole = false });

            var departmentRepo = new Mock<IRepository<Department>>();
            departmentRepo.Setup(x => x.GetById(30)).Returns(classDepartment);

            unitOfWork.Setup(x => x.Repository<Role>()).Returns(roleRepo.Object);
            unitOfWork.Setup(x => x.Repository<Department>()).Returns(departmentRepo.Object);

            var userService = new Mock<IUserService>();
            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(x => x.UserId).Returns("facilities-user");
            userService.Setup(x => x.GetById("facilities-user")).Returns(new UserVm
            {
                Id = "facilities-user",
                DepartmentId = 10,
                RoleId = 5
            });

            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(x => x.HasPermission("facilities-user", "Assets.Transfer")).Returns(true);

            var service = CreateDepartmentScopeService(
                unitOfWork.Object,
                currentUser.Object,
                userService.Object,
                false,
                authorization.Object);

            Assert.DoesNotThrow(() => service.EnsureCanAccessAsset(asset));
        }

        [Test]
        public void EnsureCanAccessAsset_BlocksOtherAdminDepartment_ForTransferUser()
        {
            var asset = new Asset { Id = 2, DepartmentId = 20, IsActive = true, AssetTag = "IT-001", CurrentStatus = AssetStatus.InStore };
            var itDepartment = new Department { Id = 20, Name = "IT", IsActive = true, DepartmentKind = DepartmentKind.Administrative };

            var unitOfWork = new Mock<IUnitOfWork>();
            var roleRepo = new Mock<IRepository<Role>>();
            roleRepo.Setup(x => x.GetById(5)).Returns(new Role { Id = 5, Name = "Facilities Manager", IsSystemRole = false });

            var departmentRepo = new Mock<IRepository<Department>>();
            departmentRepo.Setup(x => x.GetById(20)).Returns(itDepartment);

            var transferRepo = new Mock<IRepository<AssetTransfer>>();
            transferRepo.Setup(x => x.Find(It.IsAny<System.Linq.Expressions.Expression<System.Func<AssetTransfer, bool>>>()))
                .Returns(new List<AssetTransfer>().AsQueryable());

            var disposalRepo = new Mock<IRepository<DisposalRecord>>();
            disposalRepo.Setup(x => x.Find(It.IsAny<System.Linq.Expressions.Expression<System.Func<DisposalRecord, bool>>>()))
                .Returns(new List<DisposalRecord>().AsQueryable());

            var receivingRepo = new Mock<IRepository<AssetReceiving>>();
            receivingRepo.Setup(x => x.Find(It.IsAny<System.Linq.Expressions.Expression<System.Func<AssetReceiving, bool>>>()))
                .Returns(new List<AssetReceiving>().AsQueryable());

            unitOfWork.Setup(x => x.Repository<Role>()).Returns(roleRepo.Object);
            unitOfWork.Setup(x => x.Repository<Department>()).Returns(departmentRepo.Object);
            unitOfWork.Setup(x => x.Repository<AssetTransfer>()).Returns(transferRepo.Object);
            unitOfWork.Setup(x => x.Repository<DisposalRecord>()).Returns(disposalRepo.Object);
            unitOfWork.Setup(x => x.Repository<AssetReceiving>()).Returns(receivingRepo.Object);

            var userService = new Mock<IUserService>();
            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(x => x.UserId).Returns("facilities-user");
            userService.Setup(x => x.GetById("facilities-user")).Returns(new UserVm
            {
                Id = "facilities-user",
                DepartmentId = 10,
                RoleId = 5
            });

            var authorization = new Mock<IAuthorizationService>();
            authorization.Setup(x => x.HasPermission("facilities-user", "Assets.Transfer")).Returns(true);

            var service = CreateDepartmentScopeService(
                unitOfWork.Object,
                currentUser.Object,
                userService.Object,
                false,
                authorization.Object);

            var ex = Assert.Throws<BusinessException>(() => service.EnsureCanAccessAsset(asset));
            StringAssert.Contains("another department", ex.Message);
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
            Assert.IsFalse(service.IncludesClassDepartmentAssets);
        }

        private static DepartmentScopeService CreateDepartmentScopeService(
            IUnitOfWork unitOfWork,
            ICurrentUserContext currentUser,
            IUserService userService,
            bool isCompanyAdmin,
            IAuthorizationService authorizationService = null)
        {
            return new DepartmentScopeService(
                unitOfWork,
                currentUser,
                userService,
                new FakeOrganizationScopeService(companyAdmin: isCompanyAdmin),
                authorizationService);
        }
    }
}
