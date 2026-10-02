using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentOrgHierarchyDisplayTests
    {
        [Test]
        public void BuildAdministrativeSectionItems_NestsSubsDirectRoomsAndSubRooms()
        {
            var flat = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Code = "IT", Name = "IT", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Code = "IT-LAB", Name = "Comp Lab", DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 3, Code = "IT-LAB-R1", Name = "Lab 1", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 },
                new DepartmentVm { Id = 4, Code = "IT-STORE", Name = "Store", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 5, Code = "VIS", Name = "Visitor", DepartmentKind = DepartmentKind.Room }
            };

            var adminItems = DepartmentOrgHierarchyDisplay.BuildAdministrativeSectionItems(flat);
            Assert.AreEqual(1, adminItems.Count);
            var admin = adminItems[0];
            Assert.AreEqual(2, admin.Children.Count);
            Assert.AreEqual(DepartmentKind.SubDepartment, admin.Children[0].DepartmentKind);
            Assert.AreEqual(1, admin.Children[0].Children.Count);
            Assert.AreEqual(3, admin.Children[0].Children[0].Id);
            Assert.AreEqual(DepartmentKind.Room, admin.Children[1].DepartmentKind);
            Assert.AreEqual(4, admin.Children[1].Id);
        }

        [Test]
        public void GetDirectRoomsUnderAdministrative_ExcludesRoomsUnderSubDepartments()
        {
            var flat = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Code = "ADM", Name = "Admin", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Code = "ADM-SUB", Name = "Sub", DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 3, Code = "ADM-SUB-R", Name = "Sub room", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 },
                new DepartmentVm { Id = 4, Code = "ADM-R", Name = "Direct room", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 1 }
            };

            var direct = DepartmentOrgHierarchyDisplay.GetDirectRoomsUnderAdministrative(flat, 1);
            Assert.AreEqual(1, direct.Count);
            Assert.AreEqual(4, direct[0].Id);
        }

        [Test]
        public void FormatDepartmentKind_UsesSubDepartmentLabel()
        {
            Assert.AreEqual("Sub-department", DepartmentOrgHierarchyDisplay.FormatDepartmentKind(DepartmentKind.SubDepartment));
        }
    }
}
