using System.Collections.Generic;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class SchoolClassCodeHelperTests
    {
        [Test]
        public void TryParseClass_ParsesStreamCodesFromTemplate()
        {
            int grade;
            string stream;
            Assert.IsTrue(SchoolClassCodeHelper.TryParseClass("3C", out grade, out stream));
            Assert.AreEqual(3, grade);
            Assert.AreEqual("C", stream);
        }

        [Test]
        public void BuildClassDepartmentCode_MapsTemplateClassToLeafCode()
        {
            Assert.AreEqual("G03C", SchoolClassCodeHelper.BuildClassDepartmentCode("3C"));
            Assert.AreEqual("G06B", SchoolClassCodeHelper.BuildClassDepartmentCode("6B"));
        }

        [Test]
        public void TryParseClass_RejectsGradesAboveSix()
        {
            int grade;
            string stream;
            Assert.IsFalse(SchoolClassCodeHelper.TryParseClass("7A", out grade, out stream));
        }

        [Test]
        public void TryParseClass_RejectsNonClassValues()
        {
            int grade;
            string stream;
            Assert.IsFalse(SchoolClassCodeHelper.TryParseClass("Comp Lab - Senior", out grade, out stream));
        }

        [Test]
        public void IsClassroomDepartment_MatchesTemplateDepartmentName()
        {
            Assert.IsTrue(SchoolClassCodeHelper.IsClassroomDepartment("Classroom"));
            Assert.IsFalse(SchoolClassCodeHelper.IsClassroomDepartment("Information Technology"));
        }
    }

    [TestFixture]
    public class ImportQuantityParserTests
    {
        [Test]
        public void ParseFromDescription_ParsesLeadingAndTrailingCountsFromClientRows()
        {
            Assert.AreEqual(12, ImportQuantityParser.ParseFromDescription("Long desks - 12 units"));
            Assert.AreEqual(24, ImportQuantityParser.ParseFromDescription("24 wooden chairs"));
        }

        [Test]
        public void ParseFromDescription_DefaultsToOneWhenNoCount()
        {
            Assert.AreEqual(1, ImportQuantityParser.ParseFromDescription("Finance team laptop"));
        }

        [Test]
        public void ResolveQuantity_UsesExplicitQuantityColumnWhenPresent()
        {
            var row = new Dictionary<string, string> { { "Quantity", "5" }, { "Description", "24 wooden chairs" } };
            Assert.AreEqual(5, ImportQuantityParser.ResolveQuantity(row, GetValue));
        }

        [Test]
        public void ResolveQuantity_FallsBackToDescriptionWhenQuantityMissing()
        {
            var row = new Dictionary<string, string> { { "Description", "24 wooden chairs" } };
            Assert.AreEqual(24, ImportQuantityParser.ResolveQuantity(row, GetValue));
        }

        [Test]
        public void ResolveQuantity_ThrowsForInvalidExplicitQuantity()
        {
            var row = new Dictionary<string, string> { { "Quantity", "0" } };
            Assert.Throws<BusinessException>(() => ImportQuantityParser.ResolveQuantity(row, GetValue));
        }

        private static string GetValue(IDictionary<string, string> row, string key)
        {
            string value;
            return row.TryGetValue(key, out value) ? value : null;
        }
    }
}
