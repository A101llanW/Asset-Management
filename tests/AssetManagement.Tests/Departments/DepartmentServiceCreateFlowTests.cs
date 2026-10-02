using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentServiceCreateFlowTests
    {
        [Test]
        public void CreateFromWizard_CreatesSubDepartmentUnderAdministrativeParent()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 5,
                Name = "Information Technology",
                Code = "IT",
                IsActive = true,
                DepartmentKind = DepartmentKind.Administrative,
                IsRequisitionTarget = true
            });

            var service = CreateService(unitOfWork);
            var id = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentService.SetupModeSubDepartment,
                ParentDepartmentId = 5,
                LockedParentDepartmentId = 5,
                Name = "Comp Lab - Senior"
            });

            var created = unitOfWork.Repository<Department>().GetById(id);
            Assert.AreEqual(DepartmentKind.SubDepartment, created.DepartmentKind);
            Assert.AreEqual(5, created.ParentDepartmentId);
            Assert.AreEqual("IT-COMPLABSEN", created.Code);
            Assert.IsFalse(unitOfWork.Repository<Department>().GetById(5).IsRequisitionTarget);
        }

        [Test]
        public void CreateFromWizard_CreatesIndependentRoom()
        {
            var unitOfWork = new FakeUnitOfWork();
            var service = CreateService(unitOfWork);

            var id = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentService.SetupModeRoom,
                Name = "Visitor Room",
                Code = "VISITOR",
                IsRequisitionTarget = true
            });

            var created = unitOfWork.Repository<Department>().GetById(id);
            Assert.AreEqual(DepartmentKind.Room, created.DepartmentKind);
            Assert.IsFalse(created.ParentDepartmentId.HasValue);
            Assert.AreEqual("VISITOR", created.Code);
        }

        [Test]
        public void CreateFromWizard_CreatesRoomUnderSubDepartment()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 8,
                Name = "Comp Lab - Senior",
                Code = "IT-COMPLABSE",
                IsActive = true,
                DepartmentKind = DepartmentKind.SubDepartment,
                ParentDepartmentId = 5
            });

            var service = CreateService(unitOfWork);
            var id = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentService.SetupModeRoom,
                ParentDepartmentId = 8,
                LockedParentDepartmentId = 8,
                Name = "Lab 1"
            });

            var created = unitOfWork.Repository<Department>().GetById(id);
            Assert.AreEqual(DepartmentKind.Room, created.DepartmentKind);
            Assert.AreEqual(8, created.ParentDepartmentId);
            Assert.AreEqual("ITCOMPLABS-LAB1", created.Code);
        }

        [Test]
        public void CreateFromWizard_CreatesRoomUnderAdministrativeParent()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 5,
                Name = "Facilities",
                Code = "FAC",
                IsActive = true,
                DepartmentKind = DepartmentKind.Administrative,
                IsRequisitionTarget = true
            });

            var service = CreateService(unitOfWork);
            var id = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentService.SetupModeRoom,
                ParentDepartmentId = 5,
                LockedParentDepartmentId = 5,
                Name = "Store Room"
            });

            var created = unitOfWork.Repository<Department>().GetById(id);
            Assert.AreEqual(DepartmentKind.Room, created.DepartmentKind);
            Assert.AreEqual(5, created.ParentDepartmentId);
            Assert.AreEqual("FAC-STOREROOM", created.Code);
        }

        [Test]
        public void CreateFromWizard_RejectsTamperedLockedParent()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 5,
                Name = "IT",
                Code = "IT",
                IsActive = true,
                DepartmentKind = DepartmentKind.Administrative
            });

            var service = CreateService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentService.SetupModeSubDepartment,
                ParentDepartmentId = 99,
                LockedParentDepartmentId = 5,
                Name = "Should Fail"
            }));
        }

        [Test]
        public void BuildRoomCode_CombinesParentAndRoomName()
        {
            Assert.AreEqual("ITCOMPLABS-LAB1", SchoolDepartmentCodeHelper.BuildRoomCode("IT-COMPLABSE", "Lab 1"));
        }

        private static DepartmentService CreateService(FakeUnitOfWork unitOfWork)
        {
            return new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService());
        }
    }
}
