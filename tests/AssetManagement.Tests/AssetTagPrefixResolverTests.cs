using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class AssetTagPrefixResolverTests
    {
        // Science (SCI) > Physics (PHY, sub-department) > Lab 1 (LAB1, room); Science > Store Room (STORE, room directly under the dept)
        private static Dictionary<int, Department> Tree(string sci = null, string phy = null, string lab = null)
        {
            return new List<Department>
            {
                new Department { Id = 1, Name = "Science", Code = "SCI", DepartmentKind = DepartmentKind.Administrative, AssetTagPrefix = sci },
                new Department { Id = 2, Name = "Physics", Code = "PHY", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.SubDepartment, AssetTagPrefix = phy },
                new Department { Id = 3, Name = "Lab 1", Code = "LAB1", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Room, AssetTagPrefix = lab },
                new Department { Id = 4, Name = "Store Room", Code = "STORE", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Room },
                new Department { Id = 5, Name = "Main Library", Code = null, DepartmentKind = DepartmentKind.Administrative }
            }.ToDictionary(x => x.Id);
        }

        [Test]
        public void Default_IsTopDepartmentPlusRoom()
        {
            var r = AssetTagPrefixResolver.Resolve(3, Tree(), null);
            Assert.AreEqual("SCI-LAB1", r.Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceDerivedDefault, r.Source);
            Assert.AreEqual("SCI-LAB1-001", AssetTagPrefixResolver.FormatTag(r.Prefix, 1));
            Assert.AreEqual("SCI-STORE", AssetTagPrefixResolver.Resolve(4, Tree(), null).Prefix);
        }

        [Test]
        public void Default_WithoutRoom_UsesDepartmentAndSubUnit()
        {
            Assert.AreEqual("SCI", AssetTagPrefixResolver.Resolve(1, Tree(), null).Prefix);
            Assert.AreEqual("SCI-PHY", AssetTagPrefixResolver.Resolve(2, Tree(), null).Prefix);
            Assert.AreEqual("ML", AssetTagPrefixResolver.Resolve(5, Tree(), null).Prefix, "no code: acronym of the name");
            Assert.AreEqual("GEN", AssetTagPrefixResolver.Resolve(null, Tree(), null).Prefix);
            Assert.AreEqual("GEN", AssetTagPrefixResolver.Resolve(999, Tree(), null).Prefix);
        }

        [Test]
        public void TenantPrefix_BeatsDerived_ButNotOverrides()
        {
            Assert.AreEqual("NIS", AssetTagPrefixResolver.Resolve(3, Tree(), "nis").Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceTenantDefault, AssetTagPrefixResolver.Resolve(3, Tree(), "nis").Source);
            Assert.AreEqual("SC", AssetTagPrefixResolver.Resolve(3, Tree(sci: "SC"), "nis").Prefix);
        }

        [Test]
        public void Precedence_RoomThenSubDepartmentThenDepartment()
        {
            var deptOnly = AssetTagPrefixResolver.Resolve(3, Tree(sci: "SC"), null);
            Assert.AreEqual("SC", deptOnly.Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceDepartmentOverride, deptOnly.Source);

            var sub = AssetTagPrefixResolver.Resolve(3, Tree(sci: "SC", phy: "PH"), null);
            Assert.AreEqual("PH", sub.Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceSubDepartmentOverride, sub.Source);

            var room = AssetTagPrefixResolver.Resolve(3, Tree(sci: "SC", phy: "PH", lab: "L1"), null);
            Assert.AreEqual("L1", room.Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceRoomOverride, room.Source);
            Assert.AreEqual(3, room.SourceDepartmentId);

            // Sibling room is not affected by Lab 1's override or Physics' override.
            Assert.AreEqual("SC", AssetTagPrefixResolver.Resolve(4, Tree(sci: "SC", phy: "PH", lab: "L1"), null).Prefix);
        }

        [Test]
        public void NormalizePrefix_CleansInput()
        {
            Assert.AreEqual("LAB-1", AssetTagPrefixResolver.NormalizePrefix("  lab 1 "));
            Assert.AreEqual("A-B", AssetTagPrefixResolver.NormalizePrefix("--a__b--"));
            Assert.AreEqual("ABC", AssetTagPrefixResolver.NormalizePrefix("a$b%c"));
            Assert.IsNull(AssetTagPrefixResolver.NormalizePrefix("$$$"));
            Assert.IsNull(AssetTagPrefixResolver.NormalizePrefix("   "));
            Assert.AreEqual(AssetTagPrefixResolver.MaxPrefixLength, AssetTagPrefixResolver.NormalizePrefix(new string('X', 40)).Length);
        }

        [Test]
        public void Chain_SurvivesCycles()
        {
            var tree = new Dictionary<int, Department>
            {
                { 1, new Department { Id = 1, Name = "A", Code = "A", ParentDepartmentId = 2 } },
                { 2, new Department { Id = 2, Name = "B", Code = "B", ParentDepartmentId = 1 } }
            };
            Assert.AreEqual(2, AssetTagPrefixResolver.GetChain(1, tree).Count);
            Assert.IsNotNull(AssetTagPrefixResolver.Resolve(1, tree, null).Prefix);
        }

        [Test]
        public void IsEnabled_DefaultsOn()
        {
            Assert.IsTrue(AssetTagPrefixResolver.IsEnabled(null));
            Assert.IsTrue(AssetTagPrefixResolver.IsEnabled(new Dictionary<string, string>()));
            Assert.IsFalse(AssetTagPrefixResolver.IsEnabled(new Dictionary<string, string> { { AssetTagPrefixResolver.EnabledSettingKey, "False" } }));
        }

        [Test]
        public void Sequence_ContinuesAfterExistingAndIgnoresOtherShapes()
        {
            var assets = new List<Asset>
            {
                new Asset { AssetTag = "SCI-LAB1-001" },
                new Asset { AssetTag = "SCI-LAB1-007" },
                new Asset { AssetTag = "SCI-LAB1-X09" },
                new Asset { AssetTag = "SCI-LAB1-2-050" },
                new Asset { AssetTag = "K7Q2M9XPLA" }
            }.AsQueryable();
            Assert.AreEqual(8, AssetTagHelper.GetNextSequence(assets, "SCI-LAB1-"));
            Assert.AreEqual("SCI-LAB1-1000", AssetTagPrefixResolver.FormatTag("SCI-LAB1", 1000));
        }

        // ---- rel1018b: per-level segment switches ----

        private static AssetTagPrefixResolver.Options Opt(bool dept = true, bool sub = false, bool room = true, string tenant = null)
        {
            return new AssetTagPrefixResolver.Options { TenantPrefix = tenant, UseDepartmentSegment = dept, UseSubDepartmentSegment = sub, UseRoomSegment = room };
        }

        [Test]
        public void Segments_DefaultsMatchRel1018a()
        {
            var tree = Tree();
            foreach (var id in new int?[] { 1, 2, 3, 4, 5, null })
            {
                Assert.AreEqual(AssetTagPrefixResolver.Resolve(id, tree, (string)null).Prefix, AssetTagPrefixResolver.ResolveWithOptions(id, tree, Opt()).Prefix, "id " + id);
            }

            Assert.AreEqual("SCI-LAB1", AssetTagPrefixResolver.ResolveWithOptions(3, tree, AssetTagPrefixResolver.GetOptions(new Dictionary<string, string>())).Prefix);
        }

        [Test]
        public void Segments_TenantDefaults()
        {
            var tree = Tree();
            Assert.AreEqual("SCI", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt(room: false)).Prefix, "dept + random");
            Assert.AreEqual("SCI-PHY-LAB1", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt(sub: true)).Prefix, "dept + sub + room");
            Assert.AreEqual("SCI-PHY", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt(sub: true, room: false)).Prefix);
            Assert.AreEqual("LAB1", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt(dept: false)).Prefix, "room only");
            Assert.AreEqual("SCI", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt(dept: false, room: false)).Prefix, "all off = department code");
            Assert.AreEqual("SCI-PHY", AssetTagPrefixResolver.ResolveWithOptions(2, tree, Opt(sub: true)).Prefix, "sub-unit shown once, not twice");
            Assert.AreEqual("SCI-STORE", AssetTagPrefixResolver.ResolveWithOptions(4, tree, Opt(sub: true)).Prefix, "no sub-department in the chain");
        }

        [Test]
        public void Segments_PerNodeFlagsInheritAndNearestWins()
        {
            var tree = Tree();
            tree[1].AssetTagUseRoomSegment = false;             // whole department: dept + random
            Assert.AreEqual("SCI", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt()).Prefix);
            Assert.AreEqual("SCI", AssetTagPrefixResolver.ResolveWithOptions(4, tree, Opt()).Prefix);
            tree[4].AssetTagUseRoomSegment = true;              // room B opts back in
            Assert.AreEqual("SCI-STORE", AssetTagPrefixResolver.ResolveWithOptions(4, tree, Opt()).Prefix);
            Assert.AreEqual("SCI", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt()).Prefix, "sibling room unaffected");
            tree[2].AssetTagUseSubDepartmentSegment = true;     // sub-department adds its code for its subtree
            tree[2].AssetTagUseRoomSegment = true;
            Assert.AreEqual("SCI-PHY-LAB1", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt()).Prefix);
            tree[3].AssetTagUseSubDepartmentSegment = false;
            Assert.AreEqual("SCI-LAB1", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt()).Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceDerivedDefault, AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt()).Source);
        }

        [Test]
        public void Segments_LiteralOverrideAndTenantPrefixStillWin()
        {
            var tree = Tree(phy: "PH");
            tree[3].AssetTagUseRoomSegment = true;
            tree[3].AssetTagUseSubDepartmentSegment = true;
            Assert.AreEqual("PH", AssetTagPrefixResolver.ResolveWithOptions(3, tree, Opt()).Prefix);
            Assert.AreEqual("NIS", AssetTagPrefixResolver.ResolveWithOptions(4, Tree(), Opt(sub: true, tenant: "nis")).Prefix);
        }

        [Test]
        public void Segments_SettingsParsing()
        {
            var o = AssetTagPrefixResolver.GetOptions(new Dictionary<string, string>
            {
                { AssetTagPrefixResolver.UseDepartmentSegmentSettingKey, "False" },
                { AssetTagPrefixResolver.UseSubDepartmentSegmentSettingKey, " true " },
                { AssetTagPrefixResolver.UseRoomSegmentSettingKey, "junk" }
            });
            Assert.IsFalse(o.UseDepartmentSegment);
            Assert.IsTrue(o.UseSubDepartmentSegment);
            Assert.IsTrue(o.UseRoomSegment, "unparseable = default");
        }
    }
}