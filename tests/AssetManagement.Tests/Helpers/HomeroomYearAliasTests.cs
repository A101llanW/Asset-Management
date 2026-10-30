using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>
    /// pt547u option A (display only): rooms of "Grade N homerooms" (ACADEMIC-GRADE{N}HR) show directly under
    /// Year N ("Year 7 &gt; 7A"); the homerooms level is hidden; data, routing and tags are unchanged.
    /// </summary>
    [TestFixture]
    public class HomeroomYearAliasTests
    {
        private static List<DepartmentVm> School()
        {
            return new List<DepartmentVm>
            {
                new DepartmentVm { Id = 1, Name = "Academics", Code = "ACADEMIC", DepartmentKind = DepartmentKind.Administrative, IsActive = true },
                new DepartmentVm { Id = 7, Name = "Grade 7", Code = "G07", DepartmentKind = DepartmentKind.Grade, IsActive = true },
                new DepartmentVm { Id = 10, Name = "Grade 10", Code = "G10", DepartmentKind = DepartmentKind.Grade, IsActive = true },
                new DepartmentVm { Id = 2, Name = "Grade 2", Code = "G02", DepartmentKind = DepartmentKind.Grade, IsActive = true },
                new DepartmentVm { Id = 70, Name = "Grade 7 homerooms", Code = "ACADEMIC-GRADE7HR", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 71, Name = "Grade 7B", Code = "G07B", ParentDepartmentId = 70, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 72, Name = "Grade 7A", Code = "G07A", ParentDepartmentId = 70, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true },
                new DepartmentVm { Id = 80, Name = "Science", Code = "ACADEMIC-SCI", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, IsActive = true },
                new DepartmentVm { Id = 81, Name = "Lab 1", Code = "LAB1", ParentDepartmentId = 80, DepartmentKind = DepartmentKind.Room, IsActive = true, IsRequisitionTarget = true }
            };
        }

        [TestCase("ACADEMIC-GRADE7HR", 7)]
        [TestCase("academic-grade13hr", 13)]
        [TestCase(" ACADEMIC-GRADE2HR ", 2)]
        public void ParseGroupYearNumber_Matches(string code, int expected)
        {
            Assert.AreEqual(expected, HomeroomYearAlias.ParseGroupYearNumber(code));
        }

        [TestCase("ACADEMIC-SCI")]
        [TestCase("ACADEMIC-GRADE7")]
        [TestCase(null)]
        public void ParseGroupYearNumber_NoMatch(string code)
        {
            Assert.IsNull(HomeroomYearAlias.ParseGroupYearNumber(code));
        }

        [Test]
        public void BuildMap_MapsHomeroomGroupToItsYearOnly()
        {
            var map = HomeroomYearAlias.BuildMap(School());
            Assert.AreEqual(1, map.Count);
            Assert.AreEqual(7, map[70]);
        }

        [Test]
        public void BuildMap_NoAliasWhenYearMissing()
        {
            var list = School().Where(x => x.Id != 7).ToList();
            Assert.AreEqual(0, HomeroomYearAlias.BuildMap(list).Count);
        }

        [Test]
        public void BuildMap_NoAliasWhenYearDuplicatedOrInactive()
        {
            var dup = School();
            dup.Add(new DepartmentVm { Id = 77, Name = "Year 7", Code = "Y7", DepartmentKind = DepartmentKind.Grade, IsActive = true });
            Assert.AreEqual(0, HomeroomYearAlias.BuildMap(dup).Count);

            var inactive = School();
            inactive.Single(x => x.Id == 7).IsActive = false;
            Assert.AreEqual(0, HomeroomYearAlias.BuildMap(inactive).Count);
        }

        [Test]
        public void FormatRoomUnderYear_UsesYearLabels()
        {
            Assert.AreEqual("Year 7 > 7A", HomeroomYearAlias.FormatRoomUnderYear("Grade 7", "Grade 7A"));
        }

        [Test]
        public void BuildPathLabel_SkipsHomeroomsLevel()
        {
            var label = DepartmentTreeHelper.BuildPathLabel(72, School());
            StringAssert.DoesNotContain("homerooms", label);
            StringAssert.Contains("Grade 7", label);
            StringAssert.EndsWith("Grade 7A", label);
        }

        [Test]
        public void BuildTreeOptions_HidesGroupAndPlacesRoomsUnderYear()
        {
            var options = DepartmentTreeHelper.BuildTreeOptions(School(), false);
            Assert.IsFalse(options.Any(x => x.Key == 70), "homerooms group must be hidden");
            var room = options.Single(x => x.Key == 72).Value;
            StringAssert.DoesNotContain("homerooms", room);
            StringAssert.Contains("Grade 7", room);
            var keys = options.Select(x => x.Key).ToList();
            Assert.Greater(keys.IndexOf(72), keys.IndexOf(7));
            Assert.Less(keys.IndexOf(72), keys.IndexOf(71), "7A before 7B");
            Assert.Less(keys.IndexOf(2), keys.IndexOf(10), "Year 2 before Year 10");
            Assert.IsTrue(options.Any(x => x.Key == 80), "ordinary sub-departments stay");
        }

        [Test]
        public void AncestorTrail_RoomUnderHomerooms_ShowsYear()
        {
            var tree = School();
            var trail = DepartmentTreeHelper.GetAncestorTrail(tree.Single(x => x.Id == 72), tree);
            CollectionAssert.AreEqual(new[] { 7 }, trail.Select(x => x.Key).ToArray());
            CollectionAssert.AreEqual(new[] { "Year 7" }, trail.Select(x => x.Value).ToArray());
        }

        [Test]
        public void AncestorTrail_OrdinarySubDepartment_Unchanged()
        {
            var tree = School();
            var trail = DepartmentTreeHelper.GetAncestorTrail(tree.Single(x => x.Id == 81), tree);
            CollectionAssert.AreEqual(new[] { 1, 80 }, trail.Select(x => x.Key).ToArray());
        }

        [Test]
        public void GroupRoomTargets_GroupsHomeroomsUnderYear()
        {
            var groups = RequisitionTargetRules.GroupRoomTargets(School());
            var yearGroup = groups.Single(g => g.Options.Any(o => o.DepartmentId == 72));
            Assert.AreEqual(7, yearGroup.DepartmentId);
            StringAssert.DoesNotContain("homerooms", yearGroup.Label);
            CollectionAssert.AreEqual(new[] { 72, 71 }, yearGroup.Options.Select(o => o.DepartmentId).ToArray());
            Assert.AreEqual(80, groups.Single(g => g.Options.Any(o => o.DepartmentId == 81)).DepartmentId);
        }

        [Test]
        public void AttachRoomsToYears_MovesRoomsAndHidesGroup()
        {
            var list = School();
            var academics = list.Single(x => x.Id == 1);
            academics.Children = list.Where(x => x.ParentDepartmentId == 1).ToList();
            HomeroomYearAlias.AttachRoomsToYears(list, new[] { academics });
            Assert.IsFalse(academics.Children.Any(x => x.Id == 70));
            Assert.IsTrue(academics.Children.Any(x => x.Id == 80));
            CollectionAssert.AreEqual(new[] { 72, 71 }, list.Single(x => x.Id == 7).Children.Select(x => x.Id).ToArray());
        }

        [Test]
        public void NaturalComparer_SortsYearsNumerically()
        {
            var sorted = new[] { "Year 10", "Year 2", "Year 13", "Year 7" }.OrderBy(x => x, NaturalStringComparer.Instance).ToArray();
            CollectionAssert.AreEqual(new[] { "Year 2", "Year 7", "Year 10", "Year 13" }, sorted);
        }

        [Test]
        public void RequisitionPathLabel_SkipsHomeroomsLevel()
        {
            var byId = new Dictionary<int, AssetManagement.Domain.Entities.Department>();
            foreach (var d in School())
            {
                byId[d.Id] = new AssetManagement.Domain.Entities.Department { Id = d.Id, Name = d.Name, Code = d.Code, DepartmentKind = d.DepartmentKind, ParentDepartmentId = d.ParentDepartmentId, IsActive = d.IsActive };
            }

            Assert.AreEqual("Grade 7 \u203A Grade 7A", RequisitionTargetRules.PathLabel(byId[72], byId));
            Assert.AreEqual("Academics \u203A Science \u203A Lab 1", RequisitionTargetRules.PathLabel(byId[81], byId));
        }

        [Test]
        public void Data_IsNotModified()
        {
            var list = School();
            DepartmentTreeHelper.BuildTreeOptions(list, false);
            RequisitionTargetRules.GroupRoomTargets(list);
            Assert.AreEqual(70, list.Single(x => x.Id == 72).ParentDepartmentId);
            Assert.AreEqual("Grade 7A", list.Single(x => x.Id == 72).Name);
        }
    }
}