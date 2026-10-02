using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class DepartmentHierarchyRulesTests
    {
        [Test]
        public void IsOrganizational_IncludesRoom()
        {
            Assert.IsTrue(DepartmentHierarchyRules.IsOrganizational(DepartmentKind.Room));
            Assert.IsFalse(DepartmentHierarchyRules.IsAcademic(DepartmentKind.Room));
        }

        [Test]
        public void DisplayLabel_Room()
        {
            Assert.AreEqual("Room", DepartmentHierarchyRules.DisplayLabel(DepartmentKind.Room));
        }

        [Test]
        public void AssertValidHierarchy_Room_AllowsIndependentOrAdminOrSubUnitParent()
        {
            DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, null);

            var grade = new Department { DepartmentKind = DepartmentKind.Grade };
            Assert.Throws<BusinessException>(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, grade));

            var admin = new Department { DepartmentKind = DepartmentKind.Administrative, ParentDepartmentId = null };
            DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, admin);

            var sub = new Department { DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 };
            DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.Room, sub);
        }

        [Test]
        public void AssertValidHierarchy_Room_CannotBeParentOfSubUnit()
        {
            var room = new Department { DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 1 };
            Assert.Throws<BusinessException>(() =>
                DepartmentHierarchyRules.AssertValidHierarchy(DepartmentKind.SubDepartment, room));
        }

        [Test]
        public void WouldCreateCycle_DetectsSelfAndDescendant()
        {
            Assert.IsTrue(DepartmentHierarchyRules.WouldCreateCycle(10, 10, id => null));
            Assert.IsTrue(DepartmentHierarchyRules.WouldCreateCycle(20, 10, id =>
            {
                if (id == 10) return 20;
                return null;
            }));
            Assert.IsFalse(DepartmentHierarchyRules.WouldCreateCycle(20, 10, id =>
            {
                if (id == 10) return 5;
                return null;
            }));
        }

        [Test]
        public void DisplayLabel_SubDepartment()
        {
            Assert.AreEqual("Sub-department", DepartmentHierarchyRules.DisplayLabel(DepartmentKind.SubDepartment));
        }

    }
}