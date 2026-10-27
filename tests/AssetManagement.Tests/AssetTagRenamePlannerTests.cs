using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class AssetTagRenamePlannerTests
    {
        // Science (SCI) > Physics (PHY) > Lab 1 (LAB1 room); Science > Store Room; Library (LIB, separate tree)
        private static List<Department> Departments()
        {
            return new List<Department>
            {
                new Department { Id = 1, Name = "Science", Code = "SCI", DepartmentKind = DepartmentKind.Administrative },
                new Department { Id = 2, Name = "Physics", Code = "PHY", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment },
                new Department { Id = 3, Name = "Lab 1", Code = "LAB1", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Room },
                new Department { Id = 4, Name = "Store Room", Code = "STORE", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Room },
                new Department { Id = 6, Name = "Library", Code = "LIB", DepartmentKind = DepartmentKind.Administrative }
            };
        }

        private static AssetTagRenameCandidate C(int id, string tag, int? dept)
        {
            return new AssetTagRenameCandidate { AssetId = id, AssetTag = tag, AssetName = "Asset " + id, DepartmentId = dept };
        }

        private static List<AssetTagRenamePlanItem> Plan(IEnumerable<AssetTagRenameCandidate> candidates, string tenant = null,
            Dictionary<string, int> next = null, Dictionary<string, string[]> taken = null)
        {
            return AssetTagRenamePlanner.BuildPlan(
                candidates,
                Departments().ToDictionary(x => x.Id),
                tenant,
                p => next != null && next.ContainsKey(p) ? next[p] : 1,
                p => new HashSet<string>(taken != null && taken.ContainsKey(p) ? taken[p] : new string[0], StringComparer.OrdinalIgnoreCase));
        }

        [Test]
        public void Plan_HonoursSegmentSwitches()
        {
            var departments = Departments().ToDictionary(x => x.Id);
            departments[1].AssetTagUseRoomSegment = false; // Science: department segment only
            var candidates = new[] { C(1, "SCI-LAB1-001", 3), C(2, "SCI-001", 4), C(3, "OLD-9", 6) };
            var plan = AssetTagRenamePlanner.BuildPlan(candidates, departments, new AssetTagPrefixResolver.Options(), p => 1, p => new HashSet<string>());
            Assert.AreEqual(2, plan.Count, "SCI-001 already conforms");
            Assert.AreEqual("SCI", plan.Single(x => x.AssetId == 1).Prefix);
            Assert.AreEqual("LIB", plan.Single(x => x.AssetId == 3).Prefix);

            var withSub = AssetTagRenamePlanner.BuildPlan(new[] { C(1, "SCI-LAB1-001", 3) }, Departments().ToDictionary(x => x.Id),
                new AssetTagPrefixResolver.Options { UseSubDepartmentSegment = true }, p => 1, p => new HashSet<string>());
            Assert.AreEqual("SCI-PHY-LAB1-001", withSub.Single().NewTag);
        }

        [Test]
        public void Subtree_IncludesSubDepartmentsAndRooms_NotSiblings()
        {
            var ids = AssetTagRenamePlanner.GetSubtree(1, Departments());
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4 }, ids);
            CollectionAssert.AreEquivalent(new[] { 2, 3 }, AssetTagRenamePlanner.GetSubtree(2, Departments()));
            CollectionAssert.AreEquivalent(new[] { 3 }, AssetTagRenamePlanner.GetSubtree(3, Departments()));
        }

        [Test]
        public void Subtree_IsCycleSafe()
        {
            var list = new List<Department>
            {
                new Department { Id = 1, ParentDepartmentId = 2 },
                new Department { Id = 2, ParentDepartmentId = 1 }
            };
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, AssetTagRenamePlanner.GetSubtree(1, list));
        }

        [Test]
        public void Plan_UsesEachAssetsResolvedPrefix()
        {
            var plan = Plan(new[] { C(10, "X7K2P9QZ1A", 3), C(11, "OLD-1", 4), C(12, "abc", 2), C(13, "LEGACY", null) });
            Assert.AreEqual("SCI-PHY-001", plan.Single(x => x.AssetId == 12).NewTag);
            Assert.AreEqual("SCI-LAB1-001", plan.Single(x => x.AssetId == 10).NewTag);
            Assert.AreEqual("SCI-STORE-001", plan.Single(x => x.AssetId == 11).NewTag);
            Assert.AreEqual("GEN-001", plan.Single(x => x.AssetId == 13).NewTag);
            Assert.AreEqual("X7K2P9QZ1A", plan.Single(x => x.AssetId == 10).OldTag);
        }

        [Test]
        public void Plan_SkipsTagsAlreadyInTargetFormat()
        {
            var plan = Plan(new[] { C(1, "SCI-LAB1-004", 3), C(2, "sci-lab1-12", 3), C(3, "SCI-LAB1-X", 3), C(4, "SCI-004", 3) });
            CollectionAssert.AreEquivalent(new[] { 3, 4 }, plan.Select(x => x.AssetId).ToList());
        }

        [Test]
        public void Plan_NumbersSequentiallyFromCounterAndSkipsTakenTagsAndAliases()
        {
            var plan = Plan(
                new[] { C(1, "A1", 3), C(2, "A2", 3), C(3, "A3", 3) },
                next: new Dictionary<string, int> { { "SCI-LAB1", 7 } },
                taken: new Dictionary<string, string[]> { { "SCI-LAB1", new[] { "SCI-LAB1-008" } } });
            CollectionAssert.AreEqual(new[] { "SCI-LAB1-007", "SCI-LAB1-009", "SCI-LAB1-010" }, plan.Select(x => x.NewTag).ToList());
            Assert.AreEqual(plan.Count, plan.Select(x => x.NewTag).Distinct(StringComparer.OrdinalIgnoreCase).Count(), "new tags are unique");
        }

        [Test]
        public void Plan_TenantPrefixAppliesWhenNoOverride_AndIsStableOrder()
        {
            var a = Plan(new[] { C(2, "B", 6), C(1, "A", 6), C(3, "C", 1) }, tenant: "nis");
            var b = Plan(new[] { C(3, "C", 1), C(1, "A", 6), C(2, "B", 6) }, tenant: "nis");
            CollectionAssert.AreEqual(a.Select(x => x.AssetId + x.NewTag).ToList(), b.Select(x => x.AssetId + x.NewTag).ToList());
            Assert.IsTrue(a.All(x => x.NewTag.StartsWith("NIS-")));
            CollectionAssert.AreEqual(new[] { "NIS-001", "NIS-002", "NIS-003" }, a.Select(x => x.NewTag).ToList());
        }

        [Test]
        public void Plan_IgnoresBlankTags_AndEmptyInput()
        {
            Assert.AreEqual(0, Plan(new[] { C(1, null, 3), C(2, "  ", 3) }).Count);
            Assert.AreEqual(0, Plan(null).Count);
        }

        [Test]
        public void AlreadyConforms_IsCaseInsensitiveAndExact()
        {
            Assert.IsTrue(AssetTagRenamePlanner.AlreadyConforms(" SCI-001 ", "SCI"));
            Assert.IsTrue(AssetTagRenamePlanner.AlreadyConforms("sci-1", "SCI"));
            Assert.IsFalse(AssetTagRenamePlanner.AlreadyConforms("SCI-PHY-001", "SCI"));
            Assert.IsFalse(AssetTagRenamePlanner.AlreadyConforms("XSCI-001", "SCI"));
            Assert.IsFalse(AssetTagRenamePlanner.AlreadyConforms(null, "SCI"));
        }
    }
}