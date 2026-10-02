using System;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Services
{
    [TestFixture]
    public class DepartmentParentUpdateTests
    {
        [Test]
        public void Update_ConvertsLeafAdminToRoomAndSetsParent()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 1,
                Name = "Support",
                Code = "SUPPORT",
                DepartmentKind = DepartmentKind.Administrative,
                IsRequisitionTarget = true,
                IsActive = true
            });
            unitOfWork.Seed(new Department
            {
                Id = 2,
                Name = "Dining",
                Code = "DINING",
                DepartmentKind = DepartmentKind.Administrative,
                IsRequisitionTarget = true,
                IsActive = true
            });

            var service = new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService());

            service.Update(new DepartmentVm
            {
                Id = 2,
                Name = "Dining",
                Code = "DINING",
                DepartmentKind = DepartmentKind.Administrative,
                ParentDepartmentId = 1,
                IsRequisitionTarget = true,
                IsActive = true,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            });

            var dining = unitOfWork.Repository<Department>().GetById(2);
            var support = unitOfWork.Repository<Department>().GetById(1);
            Assert.AreEqual(DepartmentKind.Room, dining.DepartmentKind);
            Assert.AreEqual(1, dining.ParentDepartmentId);
            Assert.IsTrue(dining.IsRequisitionTarget);
            Assert.IsFalse(support.IsRequisitionTarget);
        }

        [Test]
        public void Update_RejectsDescendantParent()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 1,
                Name = "Academics",
                Code = "ACAD",
                DepartmentKind = DepartmentKind.Administrative,
                IsRequisitionTarget = false,
                IsActive = true
            });
            unitOfWork.Seed(new Department
            {
                Id = 2,
                Name = "Science",
                Code = "SCI",
                ParentDepartmentId = 1,
                DepartmentKind = DepartmentKind.SubDepartment,
                IsRequisitionTarget = false,
                IsActive = true
            });
            unitOfWork.Seed(new Department
            {
                Id = 3,
                Name = "Lab",
                Code = "LAB",
                ParentDepartmentId = 2,
                DepartmentKind = DepartmentKind.Room,
                IsRequisitionTarget = true,
                IsActive = true
            });

            var service = new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService());

            Assert.Throws<BusinessException>(() => service.Update(new DepartmentVm
            {
                Id = 2,
                Name = "Science",
                Code = "SCI",
                DepartmentKind = DepartmentKind.SubDepartment,
                ParentDepartmentId = 3,
                IsRequisitionTarget = false,
                IsActive = true,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            }));
        }

        [Test]
        public void Update_IgnoresParentChangeForGrade()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department
            {
                Id = 10,
                Name = "Grade 2",
                Code = "G2",
                DepartmentKind = DepartmentKind.Grade,
                IsRequisitionTarget = false,
                IsActive = true
            });

            var service = new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService());

            service.Update(new DepartmentVm
            {
                Id = 10,
                Name = "Grade 2",
                Code = "G2",
                DepartmentKind = DepartmentKind.Grade,
                ParentDepartmentId = 99,
                IsRequisitionTarget = true,
                IsActive = true
            });

            var grade = unitOfWork.Repository<Department>().GetById(10);
            Assert.IsFalse(grade.ParentDepartmentId.HasValue);
            Assert.IsFalse(grade.IsRequisitionTarget);
        }
    }
}
