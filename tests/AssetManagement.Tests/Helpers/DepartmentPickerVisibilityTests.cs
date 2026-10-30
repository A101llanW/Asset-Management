using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web.Mvc;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>
    /// Allan t550s2044 (display only): the "Grade N homerooms" Details page opens Year N, which lists its rooms; and
    /// department pickers hide inactive departments and children of inactive parents (17C under Grade 17) unless selected.
    /// </summary>
    [TestFixture]
    public class DepartmentPickerVisibilityTests
    {
        private static List<DepartmentVm> School()
        {
            return new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Name = "Academics", Code = "ACADEMIC", DepartmentKind = DepartmentKind.Administrative, IsActive = true },
                new DepartmentVm { Id = 7, Name = "Grade 7", Code = "G07", DepartmentKind = DepartmentKind.Grade, IsActive = true },
                new DepartmentVm { Id = 10, Name = "Grade 10", Code = "G10", DepartmentKind = DepartmentKind.Grade, IsActive = true },
                new DepartmentVm { Id = 70, Name = "Grade 7 homerooms", Code = "ACADEMIC-GRADE7HR", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 74, Name = "Grade 7D", Code = "G07D", ParentDepartmentId = 70, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 71, Name = "Grade 7B", Code = "G07B", ParentDepartmentId = 70, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 73, Name = "Grade 7C", Code = "G07C", ParentDepartmentId = 70, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 72, Name = "Grade 7A", Code = "G07A", ParentDepartmentId = 70, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 80, Name = "Science", Code = "ACADEMIC-SCI", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 81, Name = "Lab 1", Code = "LAB1", ParentDepartmentId = 80, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 17, Name = "Grade 17", Code = "G17", DepartmentKind = DepartmentKind.Grade, IsActive = false },
                new DepartmentVm { Id = 170, Name = "17C", Code = "17C", ParentDepartmentId = 17, DepartmentKind = DepartmentKind.Class, IsActive = true },
                new DepartmentVm { Id = 90, Name = "Old Block", Code = "OLD", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, IsActive = false },
                new DepartmentVm { Id = 91, Name = "Old Room", Code = "OLDR", ParentDepartmentId = 90, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true }
            };
        }

        [Test]
        public void RedirectYearId_HomeroomsGroupOpensItsYear()
        {
            Assert.AreEqual(7, HomeroomYearAlias.RedirectYearId(70, School()));
        }

        [TestCase(7)]
        [TestCase(80)]
        [TestCase(72)]
        [TestCase(999)]
        public void RedirectYearId_NullForOtherDepartments(int id)
        {
            Assert.IsNull(HomeroomYearAlias.RedirectYearId(id, School()));
        }

        [Test]
        public void RedirectYearId_NullWhenYearMissing()
        {
            Assert.IsNull(HomeroomYearAlias.RedirectYearId(70, School().Where(x => x.Id != 7)));
        }

        [Test]
        public void RoomsForYear_ListsGroupRoomsInNaturalOrder()
        {
            var rooms = HomeroomYearAlias.RoomsForYear(7, School());
            CollectionAssert.AreEqual(new[] { 72, 71, 73, 74 }, rooms.Select(x => x.Id).ToArray());
        }

        [Test]
        public void RoomsForYear_IncludesDirectChildrenAndNotTheGroup()
        {
            var list = School();
            list.Add(new DepartmentVm { Id = 75, Name = "Grade 7E", Code = "G07E", ParentDepartmentId = 7, DepartmentKind = DepartmentKind.Class, IsActive = true });
            var ids = HomeroomYearAlias.RoomsForYear(7, list).Select(x => x.Id).ToList();
            CollectionAssert.AreEqual(new[] { 72, 71, 73, 74, 75 }, ids);
            CollectionAssert.DoesNotContain(ids, 70);
        }

        [Test]
        public void RoomsForYear_EmptyForYearWithoutRooms()
        {
            Assert.AreEqual(0, HomeroomYearAlias.RoomsForYear(10, School()).Count);
        }

        [Test]
        public void HiddenIds_InactiveAndChildrenOfInactiveParents()
        {
            var hidden = DepartmentPickerVisibility.HiddenIds(School());
            CollectionAssert.AreEquivalent(new[] { 17, 170, 90, 91 }, hidden.ToArray());
        }

        [Test]
        public void HiddenIds_CycleSafe()
        {
            var list = new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Name = "A", ParentDepartmentId = 2, IsActive = true },
                new DepartmentVm { Id = 2, Name = "B", ParentDepartmentId = 1, IsActive = true }
            };
            Assert.AreEqual(0, DepartmentPickerVisibility.HiddenIds(list).Count);
        }

        [Test]
        public void VisibleForPicker_KeepsSelectedHiddenValue()
        {
            var ids = DepartmentPickerVisibility.VisibleForPicker(School(), 170).Select(x => x.Id).ToList();
            CollectionAssert.Contains(ids, 170);
            CollectionAssert.DoesNotContain(ids, 17);
            CollectionAssert.DoesNotContain(ids, 91);
        }

        [Test]
        public void BuildTreeOptions_SkipsChildrenOfInactiveParents()
        {
            var ids = DepartmentTreeHelper.BuildTreeOptions(School(), false).Select(x => x.Key).ToList();
            CollectionAssert.DoesNotContain(ids, 170);
            CollectionAssert.DoesNotContain(ids, 91);
            CollectionAssert.Contains(ids, 72);
            CollectionAssert.Contains(ids, 81);
        }

        [Test]
        public void GroupRoomTargets_SkipsRoomsUnderInactiveParents()
        {
            var ids = RequisitionTargetRules.GroupRoomTargets(School())
                .SelectMany(g => g.Options)
                .Select(r => r.DepartmentId)
                .ToList();
            CollectionAssert.DoesNotContain(ids, 91);
            CollectionAssert.Contains(ids, 81);
        }

        private static List<SelectListItem> Flat(IEnumerable<DepartmentVm> departments, int? selected)
        {
            var method = typeof(BaseController).GetMethod("BuildFlatDepartmentItems", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);
            return (List<SelectListItem>)method.Invoke(null, new object[] { departments, selected });
        }

        [Test]
        public void FlatPicker_HidesInactiveAndChildrenOfInactiveParents()
        {
            var values = Flat(School(), null).Select(x => x.Value).ToList();
            CollectionAssert.DoesNotContain(values, "170");
            CollectionAssert.DoesNotContain(values, "17");
            CollectionAssert.DoesNotContain(values, "90");
            CollectionAssert.DoesNotContain(values, "91");
            CollectionAssert.Contains(values, "72");
        }

        [Test]
        public void FlatPicker_KeepsSelectedInactiveValueLabelled()
        {
            var item = Flat(School(), 170).SingleOrDefault(x => x.Value == "170");
            Assert.IsNotNull(item);
            StringAssert.EndsWith("(inactive)", item.Text);
        }
    }
}