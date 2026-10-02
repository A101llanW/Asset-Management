using System.Collections.Generic;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentHierarchyAssertTests
    {
        [Test]
        public void AssertValidHierarchy_RejectsRoomUnderRoom()
        {
            var departments = new List<Department>
            {
                new Department { Id = 10, Code = "R1", Name = "Room 1", DepartmentKind = DepartmentKind.Room },
                new Department { Id = 11, Code = "R2", Name = "Room 2", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 10 }
            };

            Assert.Throws<BusinessException>(() => DepartmentHierarchyRules.AssertValidHierarchy(
                11,
                DepartmentKind.Room,
                10,
                departments));
        }

        [Test]
        public void AssertValidHierarchy_AllowsRoomUnderSubDepartment()
        {
            var departments = new List<Department>
            {
                new Department { Id = 1, Code = "IT", Name = "IT", DepartmentKind = DepartmentKind.Administrative },
                new Department { Id = 2, Code = "IT-LAB", Name = "Lab", DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                new Department { Id = 3, Code = "R1", Name = "Room", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 }
            };

            DepartmentHierarchyRules.AssertValidHierarchy(3, DepartmentKind.Room, 2, departments);
        }

        [Test]
        public void AssertValidHierarchy_RejectsRoomUnderClass()
        {
            var departments = new List<Department>
            {
                new Department { Id = 1, Code = "G1", Name = "Grade 1", DepartmentKind = DepartmentKind.Grade },
                new Department { Id = 2, Code = "1A", Name = "1A", DepartmentKind = DepartmentKind.Class, ParentDepartmentId = 1 },
                new Department { Id = 3, Code = "R1", Name = "Room", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 }
            };

            Assert.Throws<BusinessException>(() => DepartmentHierarchyRules.AssertValidHierarchy(
                3,
                DepartmentKind.Room,
                2,
                departments));
        }
    
        [Test]
        public void AssertValidHierarchy_AllowsDuplicateIdsInEnumeration()
        {
            // Mirrors GetById-tracked entity appearing twice after EnsureLoaded before the merge fix.
            var shared = new Department { Id = 3, Code = "R1", Name = "Room", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 };
            var departments = new List<Department>
            {
                new Department { Id = 1, Code = "IT", Name = "IT", DepartmentKind = DepartmentKind.Administrative },
                new Department { Id = 2, Code = "IT-LAB", Name = "Lab", DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                shared,
                shared
            };

            Assert.DoesNotThrow(() => DepartmentHierarchyRules.AssertValidHierarchy(
                3,
                DepartmentKind.Room,
                2,
                departments));
        }
    }
}