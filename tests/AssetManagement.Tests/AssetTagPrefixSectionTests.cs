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
    /// <summary>Section-aware tag prefixes (Prep tags like PP-GALA-001 never mix with G2-G2A-001).</summary>
    [TestFixture]
    public class AssetTagPrefixSectionTests
    {
        private static Dictionary<int, Department> Tree(string gradeOverride = null, string roomOverride = null)
        {
            return new List<Department>
            {
                // Senior: Grade 2 > G2A (class)
                new Department { Id = 1, Name = "Grade 2", Code = "G2", DepartmentKind = DepartmentKind.Grade, AssetTagPrefix = gradeOverride },
                new Department { Id = 2, Name = "G2A", Code = "G2A", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Class },
                // Prep: independent room "Gala Hall", and a Prep class under the Prep grade
                new Department { Id = 10, Name = "Prep", Code = "G00", DepartmentKind = DepartmentKind.Grade, Section = DepartmentSection.Prep },
                new Department { Id = 11, Name = "Prep Red", Code = "G00R", ParentDepartmentId = 10, DepartmentKind = DepartmentKind.Class, Section = DepartmentSection.Prep },
                new Department { Id = 12, Name = "Gala Hall", Code = "GALA", DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep, AssetTagPrefix = roomOverride },
                new Department { Id = 13, Name = "Senior Hall", Code = "SHALL", DepartmentKind = DepartmentKind.Room }
            }.ToDictionary(x => x.Id);
        }

        private static IDictionary<DepartmentSection, string> Prefixes(string prep, string senior = null)
        {
            var d = new Dictionary<DepartmentSection, string>();
            if (prep != null) d[DepartmentSection.Prep] = prep;
            if (senior != null) d[DepartmentSection.SeniorSchool] = senior;
            return d;
        }

        [Test]
        public void PrepSectionPrefix_GivesPrepGalaTags_AndSeniorTagsAreUntouched()
        {
            var prep = AssetTagPrefixResolver.Resolve(12, Tree(), null, Prefixes("PP"));
            Assert.AreEqual("PP-GALA", prep.Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceSectionPrefix, prep.Source);
            Assert.AreEqual("PP-GALA-001", AssetTagPrefixResolver.FormatTag(prep.Prefix, 1));

            var senior = AssetTagPrefixResolver.Resolve(2, Tree(), null, Prefixes("PP"));
            Assert.AreEqual("G2-G2A", senior.Prefix, "a Prep prefix must not touch Senior school");
            Assert.AreEqual(AssetTagPrefixResolver.SourceDerivedDefault, senior.Source);
        }

        [Test]
        public void PrepClass_UsesTheClassAsTheRoomSegment()
        {
            Assert.AreEqual("PP-G00R", AssetTagPrefixResolver.Resolve(11, Tree(), null, Prefixes("PP")).Prefix);
        }

        [Test]
        public void SectionPrefixOnBothSections()
        {
            var p = Prefixes("PP", "SS");
            Assert.AreEqual("PP-GALA", AssetTagPrefixResolver.Resolve(12, Tree(), null, p).Prefix);
            Assert.AreEqual("SS-SHALL", AssetTagPrefixResolver.Resolve(13, Tree(), null, p).Prefix);
            Assert.AreEqual("SS-G2A", AssetTagPrefixResolver.Resolve(2, Tree(), null, p).Prefix);
        }

        [Test]
        public void Precedence_NodeOverrideBeatsSectionPrefix_SectionPrefixBeatsTenantDefault()
        {
            var overrideWins = AssetTagPrefixResolver.Resolve(12, Tree(roomOverride: "GL"), "NIS", Prefixes("PP"));
            Assert.AreEqual("GL", overrideWins.Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceRoomOverride, overrideWins.Source);

            var gradeOverride = AssetTagPrefixResolver.Resolve(2, Tree(gradeOverride: "GR2"), "NIS", Prefixes(null, "SS"));
            Assert.AreEqual("GR2", gradeOverride.Prefix);

            var sectionBeatsTenant = AssetTagPrefixResolver.Resolve(12, Tree(), "NIS", Prefixes("PP"));
            Assert.AreEqual("PP-GALA", sectionBeatsTenant.Prefix);

            // Senior has no section prefix: the tenant default still applies there, exactly as before.
            var tenantStill = AssetTagPrefixResolver.Resolve(2, Tree(), "NIS", Prefixes("PP"));
            Assert.AreEqual("NIS", tenantStill.Prefix);
            Assert.AreEqual(AssetTagPrefixResolver.SourceTenantDefault, tenantStill.Source);
        }

        [Test]
        public void NoSectionPrefixes_BehavesExactlyLikeTheThreeArgumentResolve()
        {
            foreach (var id in new[] { 1, 2, 10, 11, 12, 13 })
            {
                foreach (var tenant in new string[] { null, "NIS" })
                {
                    var old = AssetTagPrefixResolver.Resolve(id, Tree(), tenant);
                    Assert.AreEqual(old.Prefix, AssetTagPrefixResolver.Resolve(id, Tree(), tenant, null).Prefix);
                    Assert.AreEqual(old.Prefix, AssetTagPrefixResolver.Resolve(id, Tree(), tenant, Prefixes(null)).Prefix);
                    Assert.AreEqual(old.Source, AssetTagPrefixResolver.Resolve(id, Tree(), tenant, new Dictionary<DepartmentSection, string>()).Source);
                }
            }
        }

        [Test]
        public void SectionPrefix_IsNotRepeatedAndStaysWithinTheMaximumLength()
        {
            var same = new Dictionary<int, Department>
            {
                { 1, new Department { Id = 1, Name = "Prep", Code = "PP", DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep } }
            };
            Assert.AreEqual("PP", AssetTagPrefixResolver.Resolve(1, same, null, Prefixes("PP")).Prefix);

            var longPrefix = new string('A', 18);
            var tree = new Dictionary<int, Department>
            {
                { 1, new Department { Id = 1, Name = "Prep Library Annex", Code = "LIBRARYANNEX", DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep } }
            };
            var r = AssetTagPrefixResolver.Resolve(1, tree, null, Prefixes(longPrefix));
            Assert.LessOrEqual(r.Prefix.Length, AssetTagPrefixResolver.MaxPrefixLength);
            Assert.IsTrue(r.Prefix.StartsWith(longPrefix));
        }

        [Test]
        public void GetSectionPrefixes_ReadsAndNormalizesTheSettings_BlankIsAbsent()
        {
            var settings = new Dictionary<string, string>
            {
                { AssetTagPrefixResolver.PrepPrefixSettingKey, " pp " },
                { AssetTagPrefixResolver.SeniorPrefixSettingKey, "  " }
            };
            var result = AssetTagPrefixResolver.GetSectionPrefixes(settings);

            Assert.AreEqual("PP", result[DepartmentSection.Prep]);
            Assert.IsFalse(result.ContainsKey(DepartmentSection.SeniorSchool));
            Assert.AreEqual(0, AssetTagPrefixResolver.GetSectionPrefixes(null).Count);
            Assert.AreEqual(0, AssetTagPrefixResolver.GetSectionPrefixes(new Dictionary<string, string>()).Count);
            Assert.AreEqual("AssetTag.Prefix.Section.Prep", AssetTagPrefixResolver.SectionSettingKey(DepartmentSection.Prep));
            Assert.AreEqual("AssetTag.Prefix.Section.Senior", AssetTagPrefixResolver.SectionSettingKey(DepartmentSection.SeniorSchool));
        }

        [Test]
        public void RenamePlanner_SectionOverload_PrepPrefixOnlyForPrepAssets_AndNullKeepsOldBehaviour()
        {
            var departments = Tree();
            Func<string, int> next = p => 1;
            Func<string, ISet<string>> taken = p => new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new[]
            {
                new AssetTagRenameCandidate { AssetId = 1, AssetTag = "OLD-1", AssetName = "Piano", DepartmentId = 12 },
                new AssetTagRenameCandidate { AssetId = 2, AssetTag = "OLD-2", AssetName = "Desk", DepartmentId = 2 }
            };

            var withSection = AssetTagRenamePlanner.BuildPlan(candidates, departments, null, next, taken, null, Prefixes("PP"));
            Assert.AreEqual("PP-GALA-001", withSection.Single(x => x.AssetId == 1).NewTag);
            Assert.AreEqual("G2-G2A-001", withSection.Single(x => x.AssetId == 2).NewTag);

            var without = AssetTagRenamePlanner.BuildPlan(candidates, departments, null, next, taken);
            var explicitNull = AssetTagRenamePlanner.BuildPlan(candidates, departments, null, next, taken, null, null);
            Assert.AreEqual(without.Select(x => x.NewTag).ToArray(), explicitNull.Select(x => x.NewTag).ToArray());
            Assert.AreEqual("GALA-001", without.Single(x => x.AssetId == 1).NewTag);
        }
    }
}
