using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    /// <summary>
    /// Allan, 2026-10-27: Grade -> Year and Class -> Classroom is DISPLAY wording only. Tags keep the G format
    /// (G&lt;n&gt;-G&lt;n&gt;&lt;letter&gt;-NNN) for Senior grades, alongside the Prep section prefix and the tag segment switches.
    /// </summary>
    [TestFixture]
    public class YearClassroomTagFormatTests
    {
        private static Dictionary<int, Department> Tree()
        {
            return new List<Department>
            {
                // Names as the screens now show them; codes unchanged (G7 / G7B, G13 / G13D).
                new Department { Id = 1, Name = "Year 7", Code = "G7", DepartmentKind = DepartmentKind.Grade },
                new Department { Id = 2, Name = "Classroom G7B", Code = "G7B", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Class },
                new Department { Id = 3, Name = "Year 13", Code = "G13", DepartmentKind = DepartmentKind.Grade },
                new Department { Id = 4, Name = "Classroom G13D", Code = "G13D", ParentDepartmentId = 3, DepartmentKind = DepartmentKind.Class },
                new Department { Id = 10, Name = "Prep", Code = "G00", DepartmentKind = DepartmentKind.Grade, Section = DepartmentSection.Prep },
                new Department { Id = 11, Name = "Prep Red", Code = "G00R", ParentDepartmentId = 10, DepartmentKind = DepartmentKind.Class, Section = DepartmentSection.Prep }
            }.ToDictionary(x => x.Id);
        }

        [Test]
        public void SeniorYearClassroom_KeepsTheGFormat_WithDefaultSwitches()
        {
            Assert.AreEqual("G7-G7B-045", AssetTagPrefixResolver.FormatTag(AssetTagPrefixResolver.Resolve(2, Tree(), null).Prefix, 45));
            Assert.AreEqual("G13-G13D-052", AssetTagPrefixResolver.FormatTag(AssetTagPrefixResolver.Resolve(4, Tree(), null).Prefix, 52));
        }

        [Test]
        public void SeniorYearClassroom_KeepsTheGFormat_WhenAPrepPrefixIsSet()
        {
            var options = new AssetTagPrefixResolver.Options
            {
                UseDepartmentSegment = true,
                UseSubDepartmentSegment = false,
                UseRoomSegment = true,
                SectionPrefixes = new Dictionary<DepartmentSection, string> { { DepartmentSection.Prep, "PP" } }
            };
            Assert.AreEqual("G7-G7B", AssetTagPrefixResolver.ResolveWithOptions(2, Tree(), options).Prefix);
            Assert.AreEqual("G13-G13D", AssetTagPrefixResolver.ResolveWithOptions(4, Tree(), options).Prefix);
            Assert.AreEqual("PP-G00R", AssetTagPrefixResolver.ResolveWithOptions(11, Tree(), options).Prefix, "only Prep gets the Prep prefix");
        }

        [Test]
        public void NoTagEverStartsWithY()
        {
            var options = new AssetTagPrefixResolver.Options
            {
                UseDepartmentSegment = true,
                UseSubDepartmentSegment = true,
                UseRoomSegment = true,
                SectionPrefixes = new Dictionary<DepartmentSection, string> { { DepartmentSection.Prep, "PP" } }
            };
            foreach (var id in new[] { 1, 2, 3, 4 })
            {
                var prefix = AssetTagPrefixResolver.ResolveWithOptions(id, Tree(), options).Prefix;
                StringAssert.StartsWith("G", prefix);
                StringAssert.DoesNotContain("Y", prefix);
            }
        }
    }
}
