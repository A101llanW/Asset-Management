using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentHierarchyRulesTests
    {
        [Test]
        public void CanCreateSubDepartmentUnder_AllowsTopLevelAdministrativeOnly()
        {
            var admin = new Department { Id = 1, IsActive = true, DepartmentKind = DepartmentKind.Administrative };
            var nestedAdmin = new Department
            {
                Id = 2,
                IsActive = true,
                DepartmentKind = DepartmentKind.Administrative,
                ParentDepartmentId = 1
            };
            var sub = new Department { Id = 3, IsActive = true, DepartmentKind = DepartmentKind.SubDepartment };

            Assert.IsTrue(DepartmentHierarchyRules.CanCreateSubDepartmentUnder(admin));
            Assert.IsFalse(DepartmentHierarchyRules.CanCreateSubDepartmentUnder(nestedAdmin));
            Assert.IsFalse(DepartmentHierarchyRules.CanCreateSubDepartmentUnder(sub));
        }

        [Test]
        public void CanCreateRoomUnderSubDepartment_AllowsActiveSubDepartmentOnly()
        {
            var sub = new Department { Id = 10, IsActive = true, DepartmentKind = DepartmentKind.SubDepartment };
            var admin = new Department { Id = 11, IsActive = true, DepartmentKind = DepartmentKind.Administrative };

            Assert.IsTrue(DepartmentHierarchyRules.CanCreateRoomUnderSubDepartment(sub));
            Assert.IsFalse(DepartmentHierarchyRules.CanCreateRoomUnderSubDepartment(admin));
        }

        [Test]
        public void CanCreateRoomUnder_AllowsAdministrativeOrSubDepartment()
        {
            var admin = new Department { Id = 1, IsActive = true, DepartmentKind = DepartmentKind.Administrative };
            var sub = new Department { Id = 2, IsActive = true, DepartmentKind = DepartmentKind.SubDepartment };
            var grade = new Department { Id = 3, IsActive = true, DepartmentKind = DepartmentKind.Grade };

            Assert.IsTrue(DepartmentHierarchyRules.CanCreateRoomUnder(admin));
            Assert.IsTrue(DepartmentHierarchyRules.CanCreateRoomUnder(sub));
            Assert.IsFalse(DepartmentHierarchyRules.CanCreateRoomUnder(grade));
        }

        [Test]
        public void IsValidLockedParentForSetupMode_MatchesCreateFlows()
        {
            var admin = new Department { Id = 1, IsActive = true, DepartmentKind = DepartmentKind.Administrative };
            var sub = new Department { Id = 2, IsActive = true, DepartmentKind = DepartmentKind.SubDepartment };

            Assert.IsTrue(DepartmentHierarchyRules.IsValidLockedParentForSetupMode(DepartmentSetupModes.SubDepartment, admin));
            Assert.IsTrue(DepartmentHierarchyRules.IsValidLockedParentForSetupMode(DepartmentSetupModes.Room, sub));
            Assert.IsTrue(DepartmentHierarchyRules.IsValidLockedParentForSetupMode(DepartmentSetupModes.Room, admin));
        }
    }
}
