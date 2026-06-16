using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class TenantIsolationTests
    {
        [Test]
        public void UserService_GetAll_ReturnsOnlyCurrentOrganizationUsers()
        {
            var orgScope = new Mock<IOrganizationScopeService>();
            orgScope.Setup(x => x.GetCurrentOrganizationId()).Returns(1);

            var queryRepo = new Mock<IUserAccountQueryRepository>();
            queryRepo.Setup(x => x.GetUsersForOrganization(1, null, true)).Returns(new List<UserVm>
            {
                new UserVm { Id = "org1-user", Email = "a@org1.test", IsActive = true }
            });

            var service = new UserService(
                Mock.Of<IUnitOfWork>(),
                Mock.Of<IAuthorizationService>(),
                orgScope.Object,
                Mock.Of<ICurrentUserContext>(),
                Mock.Of<IAuditWriter>(),
                queryRepo.Object);

            var users = new List<UserVm>(service.GetAll());

            Assert.AreEqual(1, users.Count);
            Assert.AreEqual("org1-user", users[0].Id);
            queryRepo.Verify(x => x.GetUsersForOrganization(1, null, true), Times.Once());
        }

        [Test]
        public void UserService_GetAll_WithoutOrganizationContext_ReturnsEmpty()
        {
            var orgScope = new Mock<IOrganizationScopeService>();
            orgScope.Setup(x => x.GetCurrentOrganizationId()).Returns((int?)null);

            var service = new UserService(
                Mock.Of<IUnitOfWork>(),
                Mock.Of<IAuthorizationService>(),
                orgScope.Object,
                Mock.Of<ICurrentUserContext>(),
                Mock.Of<IAuditWriter>(),
                Mock.Of<IUserAccountQueryRepository>());

            Assert.AreEqual(0, new List<UserVm>(service.GetAll()).Count);
        }

        [Test]
        public void ScanLookup_DoesNotReturnAssetFromAnotherOrganization()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Asset
            {
                Id = 1,
                OrganizationId = 1,
                AssetTag = "ORG1-TAG",
                BarcodeOrQRCode = "BC-ORG1",
                IsActive = true,
                CurrentStatus = AssetStatus.InStore,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                CreatedAt = System.DateTime.UtcNow
            });
            unitOfWork.Seed(new Asset
            {
                Id = 2,
                OrganizationId = 2,
                AssetTag = "ORG2-TAG",
                BarcodeOrQRCode = "BC-ORG1",
                IsActive = true,
                CurrentStatus = AssetStatus.InStore,
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                CreatedAt = System.DateTime.UtcNow
            });

            var repo = new FakeAssetScanLookupRepository(unitOfWork);
            var org1Result = repo.FindByScanCode("BC-ORG1", 1, null);
            var org2Result = repo.FindByScanCode("BC-ORG1", 2, null);

            Assert.IsNotNull(org1Result);
            Assert.AreEqual("ORG1-TAG", org1Result.AssetTag);
            Assert.IsNotNull(org2Result);
            Assert.AreEqual("ORG2-TAG", org2Result.AssetTag);
        }

        [Test]
        public void DepartmentScope_NoDepartmentUser_SeesZeroAssets()
        {
            var unitOfWork = new Mock<IUnitOfWork>();
            var roleRepo = new Mock<IRepository<Role>>();
            roleRepo.Setup(x => x.GetById(5)).Returns(new Role { Id = 5, Name = "Staff", IsSystemRole = false });
            unitOfWork.Setup(x => x.Repository<Role>()).Returns(roleRepo.Object);

            var userService = new Mock<IUserService>();
            userService.Setup(x => x.GetById("staff-no-dept")).Returns(new UserVm
            {
                Id = "staff-no-dept",
                DepartmentId = null,
                RoleId = 5
            });

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(x => x.UserId).Returns("staff-no-dept");

            var service = new DepartmentScopeService(
                unitOfWork.Object,
                currentUser.Object,
                userService.Object,
                new FakeOrganizationScopeService(companyAdmin: false));

            var assets = new List<Asset>
            {
                new Asset { Id = 1, DepartmentId = 10, IsActive = true, AssetTag = "A1", CurrentStatus = AssetStatus.InStore },
                new Asset { Id = 2, DepartmentId = 20, IsActive = true, AssetTag = "A2", CurrentStatus = AssetStatus.InStore }
            };

            Assert.AreEqual(0, service.ApplyAssetScope(assets.AsQueryable()).Count());
        }
    }
}
