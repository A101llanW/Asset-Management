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
        public void Update_RejectsParentOnAdministrativeDepartment()
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

            // Current behaviour: strict hierarchy validation, no silent conversion of an admin department into a room.
            var ex = Assert.Throws<BusinessException>(() => service.Update(new DepartmentVm
            {
                Id = 2,
                Name = "Dining",
                Code = "DINING",
                DepartmentKind = DepartmentKind.Administrative,
                ParentDepartmentId = 1,
                IsRequisitionTarget = true,
                IsActive = true,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            }));

            Assert.AreEqual("Administrative departments must be top-level (no parent department).", ex.Message);
            var dining = unitOfWork.Repository<Department>().GetById(2);
            Assert.AreEqual(DepartmentKind.Administrative, dining.DepartmentKind);
            Assert.IsFalse(dining.ParentDepartmentId.HasValue);
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
        public void Update_RejectsParentOnGrade()
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

            var ex = Assert.Throws<BusinessException>(() => service.Update(new DepartmentVm
            {
                Id = 10,
                Name = "Grade 2",
                Code = "G2",
                DepartmentKind = DepartmentKind.Grade,
                ParentDepartmentId = 99,
                IsRequisitionTarget = true,
                IsActive = true
            }));

            Assert.AreEqual("Year departments cannot have a parent department.", ex.Message);
            var grade = unitOfWork.Repository<Department>().GetById(10);
            Assert.IsFalse(grade.ParentDepartmentId.HasValue);
            Assert.IsFalse(grade.IsRequisitionTarget);
        }
    }
}
