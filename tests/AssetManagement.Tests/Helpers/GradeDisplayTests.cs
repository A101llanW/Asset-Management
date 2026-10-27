using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using System.Linq;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class GradeDisplayTests
    {
        [TestCase("Grade 2", "Year 2")]
        [TestCase("Grade 2A", "Year 2A")]
        [TestCase("grade 10b", "year 10b")]
        [TestCase("GRADE 3", "YEAR 3")]
        [TestCase("Grades & Streams", "Years & Streams")]
        [TestCase("Grade 2 \u2192 Grade 2A", "Year 2 \u2192 Year 2A")]
        [TestCase("G02 - Grade 2", "G02 - Year 2")]
        [TestCase("Grade10", "Year10")]
        [TestCase("Upgrade lab", "Upgrade lab")]
        [TestCase("Gradebook room", "Gradebook room")]
        [TestCase("Biology Lab", "Biology Lab")]
        [TestCase("", "")]
        [TestCase(null, null)]
        [TestCase("ACADEMIC-GRADE2HR", "ACADEMIC-GRADE2HR")]
        [TestCase("ACADEMIC-GRADE2HR \u2014 Grade 2 homerooms", "ACADEMIC-GRADE2HR \u2014 Year 2 homerooms")]
        [TestCase("GRADE-2", "GRADE-2")]
        [TestCase("SCH_GRADE", "SCH_GRADE")]
        public void ToYear_ReplacesWholeWordGradeOnly(string input, string expected)
        {
            Assert.AreEqual(expected, GradeDisplay.ToYear(input));
        }

        [Test]
        public void FormatCodeName_ShowsYearButLeavesCode()
        {
            Assert.AreEqual("G02 - Year 2", DepartmentLabelHelper.FormatCodeName("G02", "Grade 2").Replace("\u2014", "-").Replace(" \u2013 ", " - "));
        }

        [Test]
        public void ReportYearLabels_RelabelDepartmentColumnsOnly_AndLeaveTheCsvRowsAlone()
        {
            var headers = new[] { "Tag", "Code", "Department", "From", "To", "Notes" };
            var stored = new[] { "ACA-G2A-001", "ACADEMIC-GRADE2HR", "Grade 2A", "Grade 9A", "Grade 9B", "Grade 2 note" };
            var rows = new System.Collections.Generic.List<System.Collections.Generic.IList<string>> { stored };

            var display = ReportYearLabels.Apply(headers, rows);

            CollectionAssert.AreEqual(
                new[] { "ACA-G2A-001", "ACADEMIC-GRADE2HR", "Year 2A", "Year 9A", "Year 9B", "Grade 2 note" },
                display[0].ToArray());
            Assert.AreEqual("Grade 2A", stored[2], "the shared CSV row keeps the stored name");
        }

        [Test]
        public void RoomGroupDisplayName_ShowsYear()
        {
            var vm = new AssetRoomGroupVm { DepartmentId = 5, Name = "Grade 4B", DepartmentKind = (int)DepartmentKind.Class };
            Assert.AreEqual("Year 4B", vm.DisplayName);
            Assert.AreEqual("Grade 4B", vm.Name, "stored name is not changed");
        }
    }
}