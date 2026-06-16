using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Tests.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Services
{
    [TestFixture]
    public class DepartmentScopeServiceTests
    {
        [Test]
        public void CountVisibleDepartments_ScopedUser_ReturnsOneActiveDepartment()
        {
            var departments = new List<Department>
            {
                new Department { Id = 1, Name = "Finance", IsActive = true },
                new Department { Id = 2, Name = "IT", IsActive = true }
            };

            var scope = CreateScopeService(departments, scopedDepartmentId: 2, isSuperAdmin: false);

            Assert.AreEqual(1, scope.CountVisibleDepartments(true));
        }

        [Test]
        public void CountVisibleDepartments_SuperAdmin_ReturnsAllActiveDepartments()
        {
            var departments = new List<Department>
            {
                new Department { Id = 1, Name = "Finance", IsActive = true },
                new Department { Id = 2, Name = "IT", IsActive = false }
            };

            var scope = CreateScopeService(departments, scopedDepartmentId: 1, isSuperAdmin: true);

            Assert.AreEqual(1, scope.CountVisibleDepartments(true));
            Assert.AreEqual(2, scope.CountVisibleDepartments(false));
        }

        private static DepartmentScopeService CreateScopeService(
            IList<Department> departments,
            int scopedDepartmentId,
            bool isSuperAdmin)
        {
            var departmentRepository = new Mock<IRepository<Department>>();
            departmentRepository.Setup(x => x.Query()).Returns(departments.AsQueryable());
            departmentRepository.Setup(x => x.GetAll()).Returns(departments);

            var roleRepository = new Mock<IRepository<Role>>();
            roleRepository.Setup(x => x.GetById(It.IsAny<object>())).Returns(new Role
            {
                Id = 1,
                Name = isSuperAdmin ? "Super Admin" : "Staff",
                IsSystemRole = isSuperAdmin
            });

            var unitOfWork = new Mock<IUnitOfWork>();
            unitOfWork.Setup(x => x.Repository<Department>()).Returns(departmentRepository.Object);
            unitOfWork.Setup(x => x.Repository<Role>()).Returns(roleRepository.Object);

            var currentUser = new Mock<ICurrentUserContext>();
            currentUser.Setup(x => x.UserId).Returns("user-1");

            var userService = new Mock<IUserService>();
            userService.Setup(x => x.GetById("user-1")).Returns(new UserVm
            {
                Id = "user-1",
                DepartmentId = scopedDepartmentId,
                RoleId = 1
            });

            return new DepartmentScopeService(unitOfWork.Object, currentUser.Object, userService.Object, new FakeOrganizationScopeService(companyAdmin: isSuperAdmin ? true : false));
        }
    }
}
