using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Controllers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>
    /// Bulk Label Print Department filter (Allan, 2026-11-02): the same tree as the other department pickers instead of
    /// the old flat alphabetical list. KG, Prep, then Years in number order with their classes under them ("Year 7 &gt; 7A",
    /// the "Grade N homerooms" level hidden), then departments with their sub-departments and rooms; inactive chains hidden.
    /// Option values stay department ids. Fixture mirrors the Test-WF (org 13) structure.
    /// </summary>
    [TestFixture]
    public class BulkPrintDepartmentOptionsTests
    {
        private static DepartmentVm D(int id, string name, string code, DepartmentKind kind, int? parentId = null, bool active = true, DepartmentSection? section = null)
        {
            return new DepartmentVm
            {
                Id = id,
                Name = name,
                Code = code,
                DepartmentKind = kind,
                ParentDepartmentId = parentId,
                IsActive = active,
                Section = section
            };
        }

        private static List<DepartmentVm> TestWf()
        {
            return new List<DepartmentVm>
            {
                // Departments (deliberately out of order).
                D(250, "Administration", "ADMIN", DepartmentKind.Administrative),
                D(1456, "Academics", "ACADEMICS", DepartmentKind.Administrative),
                D(246, "Information Technology", "IT", DepartmentKind.Administrative),
                D(299, "IT", "IT-101", DepartmentKind.Administrative, null, false),
                D(247, "Finance", "FIN", DepartmentKind.SubDepartment, 250),
                D(1484, "Science", "SCIENCE", DepartmentKind.SubDepartment, 1456),
                D(254, "Biology Lab", "BIOLAB", DepartmentKind.Room, 1484),
                D(274, "wellness", "WELLNESS", DepartmentKind.Room),

                // Years (Grade rows) and their homerooms groups under Academics.
                D(2686, "Grade 10", "G10", DepartmentKind.Grade),
                D(305, "Grade 7", "G07", DepartmentKind.Grade),
                D(1465, "Grade 2", "G02", DepartmentKind.Grade),
                D(2692, "Grade 10 homerooms", "ACADEMIC-GRADE10HR", DepartmentKind.SubDepartment, 1456),
                D(2689, "Grade 7 homerooms", "ACADEMIC-GRADE7HR", DepartmentKind.SubDepartment, 1456),
                D(2623, "Grade 2 homerooms", "ACADEMIC-GRADE2HR", DepartmentKind.SubDepartment, 1456),
                D(285, "Grade 10A", "G10A", DepartmentKind.Room, 2692),
                D(276, "Grade 7B", "G07B", DepartmentKind.Room, 2689),
                D(275, "Grade 7A", "G07A", DepartmentKind.Room, 2689),
                D(1467, "Grade 2B", "G02B", DepartmentKind.Room, 2623),
                D(1466, "Grade 2A", "G02A", DepartmentKind.Room, 2623),

                // Inactive Year with an (active) class under it: whole chain hidden.
                D(309, "Grade 17", "G17", DepartmentKind.Grade, null, false),
                D(310, "17C", "G17C", DepartmentKind.Class, 309),

                // Prep Year with a class.
                D(1601, "Grade 1", "G01", DepartmentKind.Grade, null, true, DepartmentSection.Prep),
                D(1602, "Grade 1A", "G01A", DepartmentKind.Class, 1601, true, DepartmentSection.Prep),

                // KG Year and the KG homerooms group (Galaxy / Farm rooms).
                D(2706, "KG", "KG", DepartmentKind.Grade, null, true, DepartmentSection.KG),
                D(2707, "KG homerooms", "ACADEMIC-GRADEKGHR", DepartmentKind.SubDepartment, 1456, true, DepartmentSection.KG),
                D(2710, "Galaxy C", "ACADEMICGR-GALAXYC", DepartmentKind.Room, 2707, true, DepartmentSection.KG),
                D(2708, "Galaxy A", "ACADEMICGR-GALAXYA", DepartmentKind.Room, 2707, true, DepartmentSection.KG),
                D(2714, "Farm B", "ACADEMICGR-FARMB", DepartmentKind.Room, 2707, true, DepartmentSection.KG),
                D(2713, "Farm A", "ACADEMICGR-FARMA", DepartmentKind.Room, 2707, true, DepartmentSection.KG)
            };
        }

        [Test]
        public void Options_AreTreeOrdered_KgPrepYearsByNumber_ThenDepartments()
        {
            var texts = AssetScanController.BuildDepartmentOptions(TestWf()).Select(x => x.Text).ToList();

            CollectionAssert.AreEqual(
                new[]
                {
                    "All departments",
                    "KG",
                    "KG > Farm A",
                    "KG > Farm B",
                    "KG > Galaxy A",
                    "KG > Galaxy C",
                    "Year 1",
                    "Year 1 > 1A",
                    "Year 2",
                    "Year 2 > 2A",
                    "Year 2 > 2B",
                    "Year 7",
                    "Year 7 > 7A",
                    "Year 7 > 7B",
                    "Year 10",
                    "Year 10 > 10A",
                    "Academics",
                    "Academics > Science",
                    "Academics > Science > Biology Lab",
                    "Administration",
                    "Administration > Finance",
                    "Information Technology",
                    "wellness"
                },
                texts);
        }

        [Test]
        public void Options_HideHomeroomsLevel()
        {
            var options = AssetScanController.BuildDepartmentOptions(TestWf());
            Assert.IsFalse(options.Any(x => x.Text.IndexOf("homerooms", System.StringComparison.OrdinalIgnoreCase) >= 0));
            var values = options.Select(x => x.Value).ToList();
            foreach (var groupId in new[] { 2623, 2689, 2692, 2707 })
            {
                CollectionAssert.DoesNotContain(values, groupId.ToString());
            }
        }

        [Test]
        public void Options_YearsSortNumerically_Year2BeforeYear10_AndBeforeKgClassesNever()
        {
            var texts = AssetScanController.BuildDepartmentOptions(TestWf()).Select(x => x.Text).ToList();
            Assert.Less(texts.IndexOf("Year 2"), texts.IndexOf("Year 7"));
            Assert.Less(texts.IndexOf("Year 7"), texts.IndexOf("Year 10"));
            // The old flat list put "Year 10" after "Galaxy C"; KG rooms now sit under KG, before every Year.
            Assert.Less(texts.IndexOf("KG > Galaxy C"), texts.IndexOf("Year 1"));
            Assert.Less(texts.IndexOf("Year 1 > 1A"), texts.IndexOf("Year 2"));
        }

        [Test]
        public void Options_HideInactiveChains()
        {
            var values = AssetScanController.BuildDepartmentOptions(TestWf()).Select(x => x.Value).ToList();
            CollectionAssert.DoesNotContain(values, "299");
            CollectionAssert.DoesNotContain(values, "309");
            CollectionAssert.DoesNotContain(values, "310");
        }

        [Test]
        public void Options_ValuesAreDepartmentIds_AllDepartmentsFirstAndBlank()
        {
            var options = AssetScanController.BuildDepartmentOptions(TestWf());
            Assert.AreEqual(string.Empty, options[0].Value);
            Assert.AreEqual("All departments", options[0].Text);
            var byText = options.Skip(1).ToDictionary(x => x.Text, x => x.Value);
            Assert.AreEqual("275", byText["Year 7 > 7A"]);
            Assert.AreEqual("305", byText["Year 7"]);
            Assert.AreEqual("2708", byText["KG > Galaxy A"]);
            Assert.AreEqual("254", byText["Academics > Science > Biology Lab"]);
            Assert.AreEqual(options.Count - 1, options.Skip(1).Select(x => x.Value).Distinct().Count());
        }

        [Test]
        public void Options_EmptyOrNullList_OnlyAllDepartments()
        {
            Assert.AreEqual(1, AssetScanController.BuildDepartmentOptions((IEnumerable<DepartmentVm>)null).Count);
            Assert.AreEqual(0, AssetScanController.BuildDepartmentOptions((AssetManagement.Application.Contracts.IDepartmentService)null).Count());
        }

        [Test]
        public void ResolveDepartmentIds_YearWithSubDepartments_CoversItsHomeroomRooms()
        {
            var list = TestWf();
            CollectionAssert.AreEqual(new[] { 275, 276, 305, 2689 }, PrintCandidateQuery.ResolveDepartmentIds(305, true, list).ToArray());
            CollectionAssert.AreEqual(new[] { 2707, 2708, 2710, 2713, 2714, 2706 }.OrderBy(x => x).ToArray(), PrintCandidateQuery.ResolveDepartmentIds(2706, true, list).ToArray());
            CollectionAssert.AreEqual(new[] { 305 }, PrintCandidateQuery.ResolveDepartmentIds(305, false, list).ToArray());
            CollectionAssert.AreEqual(new[] { 275 }, PrintCandidateQuery.ResolveDepartmentIds(275, true, list).ToArray());
        }

        [Test]
        public void ResolveDepartmentIds_DepartmentSubtreeUnchanged_AndUnknownIdStaysAlone()
        {
            var list = TestWf();
            var academics = PrintCandidateQuery.ResolveDepartmentIds(1456, true, list);
            CollectionAssert.IsSubsetOf(new[] { 1456, 1484, 254, 2689, 275, 276, 2707, 2708 }, academics);
            CollectionAssert.DoesNotContain(academics, 305);
            CollectionAssert.AreEqual(new[] { 250, 247 }.OrderBy(x => x).ToArray(), PrintCandidateQuery.ResolveDepartmentIds(250, true, list).ToArray());
            // Not in the caller's (company-scoped) list: nothing else is added.
            CollectionAssert.AreEqual(new[] { 99999 }, PrintCandidateQuery.ResolveDepartmentIds(99999, true, list).ToArray());
        }
    }
}