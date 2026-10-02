using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentServiceUpdateHierarchyTests
    {
        [Test]
        public void Update_RejectsRoomParentedByAnotherRoom()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 10, Code = "R1", Name = "Room 1", DepartmentKind = DepartmentKind.Room, IsActive = true });
            unitOfWork.Seed(new Department { Id = 11, Code = "R2", Name = "Room 2", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 10, IsActive = true });

            var service = new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService());

            Assert.Throws<BusinessException>(() => service.Update(new DepartmentVm
            {
                Id = 11,
                Name = "Room 2",
                Code = "R2",
                DepartmentKind = DepartmentKind.Room,
                ParentDepartmentId = 10,
                IsActive = true,
                IsRequisitionTarget = true
            }));
        }
    }
}
