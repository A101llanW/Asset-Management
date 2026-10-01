using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    [TestFixture]
    public class DepartmentRoomParentCandidatesTests
    {
        [Test]
        public void GetRoomParentCandidates_IncludesSubsUnderAdminAncestorAndDirectAdminOrphan()
        {
            var flat = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Code = "IT", Name = "IT", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Code = "IT-LAB", Name = "Lab", DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 3, Code = "IT-STORE", Name = "Store", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 1 }
            };

            var room = flat[2];
            var candidates = DepartmentRoomParentCandidates.GetRoomParentCandidates(room, flat);
            Assert.AreEqual(2, candidates.Count);
            Assert.AreEqual(1, candidates[0].Id);
            Assert.AreEqual(2, candidates[1].Id);
        }

        [Test]
        public void GetRoomParentCandidates_ExcludesInvalidRoomParentFromOrphanKeepCurrent()
        {
            var flat = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Code = "IT", Name = "IT", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Code = "R1", Name = "Room 1", DepartmentKind = DepartmentKind.Room },
                new DepartmentVm { Id = 3, Code = "R2", Name = "Room 2", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 }
            };

            var candidates = DepartmentRoomParentCandidates.GetRoomParentCandidates(flat[2], flat);
            Assert.AreEqual(0, candidates.Count);
        }

        [Test]
        public void GetRoomParentCandidates_DoesNotThrowWhenScopedListHasDuplicateIds()
        {
            var flat = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Code = "IT", Name = "IT", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 1, Code = "IT", Name = "Information Technology", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Code = "IT-LAB", Name = "Lab", DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 3, Code = "R1", Name = "Room", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 2 }
            };

            var candidates = DepartmentRoomParentCandidates.GetRoomParentCandidates(flat[3], flat);
            Assert.AreEqual(1, candidates.Count);
        }

        [Test]
        public void BuildOtherParentPickerGroups_IncludesOnlyAdminAndSubDepartments()
        {
            var flat = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Code = "IT", Name = "IT", DepartmentKind = DepartmentKind.Administrative },
                new DepartmentVm { Id = 2, Code = "IT-LAB", Name = "Lab", DepartmentKind = DepartmentKind.SubDepartment, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 3, Code = "R1", Name = "Room", DepartmentKind = DepartmentKind.Room, ParentDepartmentId = 1 },
                new DepartmentVm { Id = 4, Code = "G1", Name = "Grade 1", DepartmentKind = DepartmentKind.Grade }
            };

            var groups = DepartmentRoomParentCandidates.BuildOtherParentPickerGroups(flat);
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(1, groups[0].SubDepartments.Count);
            Assert.AreEqual(2, groups[0].SubDepartments[0].SubDepartmentId);
        }
    }
}
