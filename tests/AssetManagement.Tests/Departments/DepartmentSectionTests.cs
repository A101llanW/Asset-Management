using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Departments
{
    /// <summary>Migration 106: Section (Prep / Senior school) on departments, grades, classes and rooms.</summary>
    [TestFixture]
    public class DepartmentSectionTests
    {
        private sealed class CapturingAuditWriter : IAuditWriter
        {
            public readonly List<string[]> Entries = new List<string[]>();

            public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
            {
                Entries.Add(new[] { action, entityId, oldValues, newValues });
            }
        }

        private static DepartmentService CreateService(FakeUnitOfWork unitOfWork, IAuditWriter audit = null)
        {
            return new DepartmentService(
                unitOfWork,
                new PermissiveDepartmentScopeService(),
                new FakeReferenceDataCache(),
                new FakeOrganizationScopeService(),
                audit);
        }

        private static DepartmentVm Vm(int id, string name, string code, DepartmentKind kind, DepartmentSection? section = null, int? parent = null, int? sort = null)
        {
            return new DepartmentVm
            {
                Id = id,
                Name = name,
                Code = code,
                DepartmentKind = kind,
                ParentDepartmentId = parent,
                Section = section,
                SortOrder = sort,
                IsActive = true
            };
        }

        // ---- Defaults and helper ----

        [Test]
        public void ExistingRows_DefaultToSeniorSchool()
        {
            Assert.AreEqual(DepartmentSection.SeniorSchool, new Department().Section);
            Assert.AreEqual(0, (int)DepartmentSection.SeniorSchool);
            Assert.AreEqual(1, (int)DepartmentSection.Prep);
            Assert.AreEqual(DepartmentSection.SeniorSchool, DepartmentSectionHelper.Normalize(null));
            Assert.IsNull(new Department().SortOrder);
        }

        [TestCase("Prep", DepartmentSection.Prep)]
        [TestCase(" prep ", DepartmentSection.Prep)]
        [TestCase("1", DepartmentSection.Prep)]
        [TestCase("Senior", DepartmentSection.SeniorSchool)]
        [TestCase("SeniorSchool", DepartmentSection.SeniorSchool)]
        [TestCase("Senior school", DepartmentSection.SeniorSchool)]
        [TestCase("0", DepartmentSection.SeniorSchool)]
        public void ParseOrNull_AcceptsKnownValues(string raw, DepartmentSection expected)
        {
            Assert.AreEqual(expected, DepartmentSectionHelper.ParseOrNull(raw));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        [TestCase("Junior")]
        public void ParseOrNull_BlankOrUnknownIsNull(string raw)
        {
            Assert.IsNull(DepartmentSectionHelper.ParseOrNull(raw));
        }

        [Test]
        public void Labels_RanksAndQueryValues()
        {
            Assert.AreEqual("Prep", DepartmentSectionHelper.Label(DepartmentSection.Prep));
            Assert.AreEqual("Senior school", DepartmentSectionHelper.Label(DepartmentSection.SeniorSchool));
            Assert.AreEqual("Senior school", DepartmentSectionHelper.Label(null));
            Assert.Less(DepartmentSectionHelper.Rank(DepartmentSection.Prep), DepartmentSectionHelper.Rank(DepartmentSection.SeniorSchool));
            Assert.AreEqual("Prep", DepartmentSectionHelper.ToQueryValue(DepartmentSection.Prep));
            Assert.AreEqual("SeniorSchool", DepartmentSectionHelper.ToQueryValue(DepartmentSection.SeniorSchool));
            Assert.AreEqual(new[] { DepartmentSection.Prep, DepartmentSection.SeniorSchool }, DepartmentSectionHelper.Options().Select(x => x.Key).ToArray());
        }

        [Test]
        public void GradeSortNumber_FromSortOrderElseGradeCode()
        {
            Assert.AreEqual(0, DepartmentSectionHelper.ParseGradeNumber("G00"));
            Assert.AreEqual(10, DepartmentSectionHelper.ParseGradeNumber("g10"));
            Assert.IsNull(DepartmentSectionHelper.ParseGradeNumber("PREP"));
            Assert.IsNull(DepartmentSectionHelper.ParseGradeNumber("G001"));
            Assert.AreEqual(7, DepartmentSectionHelper.EffectiveSortNumber(7, "G03"), "explicit sort number wins");
            Assert.AreEqual(3, DepartmentSectionHelper.EffectiveSortNumber(null, "G03"));
            Assert.AreEqual(int.MaxValue, DepartmentSectionHelper.EffectiveSortNumber(null, "FORM1"));
        }

        [Test]
        public void ReportIntersect_AllOfSectionWhenNothingTicked_TickedOnesInSection_SentinelWhenEmpty()
        {
            Assert.AreEqual(new[] { 1, 2 }, DepartmentSectionHelper.IntersectForReport(new[] { 1, 2 }, null).ToArray());
            Assert.AreEqual(new[] { 1, 2 }, DepartmentSectionHelper.IntersectForReport(new[] { 1, 2 }, new List<int>()).ToArray());
            Assert.AreEqual(new[] { 2 }, DepartmentSectionHelper.IntersectForReport(new[] { 1, 2 }, new List<int> { 2, 9 }).ToArray());
            Assert.AreEqual(new[] { -1 }, DepartmentSectionHelper.IntersectForReport(new[] { 1, 2 }, new List<int> { 9 }).ToArray(), "ticked departments outside the section = no data, never 'all'");
            Assert.AreEqual(new[] { -1 }, DepartmentSectionHelper.IntersectForReport(new int[0], null).ToArray(), "empty section = no data, never 'all'");
        }

        // ---- Tree: grouped by Section, Prep first, grades by sort number ----

        [Test]
        public void TreeSections_PrepGradeG00IsShown_AndComesBeforeSeniorGrades()
        {
            var service = CreateService(new FakeUnitOfWork());
            var departments = new List<DepartmentVm>
            {
                Vm(1, "Grade 1", "G01", DepartmentKind.Grade),
                Vm(2, "Prep", "G00", DepartmentKind.Grade, DepartmentSection.Prep),
                Vm(3, "Grade 2", "G02", DepartmentKind.Grade),
                Vm(4, "Prep Red", "G00R", DepartmentKind.Class, DepartmentSection.Prep, 2)
            };

            var sections = service.GetTreeSections(departments).ToList();

            Assert.AreEqual(new[] { "Prep", "Grade 1", "Grade 2" }, sections.Select(x => x.Title).ToArray());
            Assert.AreEqual(DepartmentSection.Prep, sections[0].SectionGroup);
            Assert.AreEqual(DepartmentSection.SeniorSchool, sections[1].SectionGroup);
            Assert.AreEqual(new[] { 4 }, sections[0].Items[0].Children.Select(x => x.Id).ToArray(), "the Prep grade shows its class");
        }

        [Test]
        public void TreeSections_GradesSortNumerically_NotByTheOldHardCodedList_AndNonGCodesAreShown()
        {
            var service = CreateService(new FakeUnitOfWork());
            var departments = new List<DepartmentVm>
            {
                Vm(1, "Grade 10", "G10", DepartmentKind.Grade),
                Vm(2, "Grade 2", "G02", DepartmentKind.Grade),
                Vm(3, "Form 1", "FORM1", DepartmentKind.Grade, null, null, 5),
                Vm(4, "Zeta", "ZETA", DepartmentKind.Grade)
            };

            var titles = service.GetTreeSections(departments).Select(x => x.Title).ToArray();

            // G02 (2), FORM1 (sort number 5), G10 (10), then a grade with no number last.
            Assert.AreEqual(new[] { "Grade 2", "Form 1", "Grade 10", "Zeta" }, titles);
        }

        [Test]
        public void TreeSections_AllSeniorData_HasOnlySeniorGroup_SoNothingChangesForExistingTenants()
        {
            var service = CreateService(new FakeUnitOfWork());
            var departments = new List<DepartmentVm>
            {
                Vm(1, "Grade 1", "G01", DepartmentKind.Grade),
                Vm(2, "G1A", "G01A", DepartmentKind.Class, null, 1),
                Vm(5, "Finance", "FIN", DepartmentKind.Administrative),
                Vm(6, "Store", "STORE", DepartmentKind.Room, null, 5)
            };

            var sections = service.GetTreeSections(departments).ToList();

            Assert.AreEqual(new[] { "Grade 1", "Administration" }, sections.Select(x => x.Title).ToArray());
            Assert.IsTrue(sections.All(x => x.SectionGroup == DepartmentSection.SeniorSchool));
        }

        [Test]
        public void TreeSections_PrepRoomsUnderASeniorAdminDepartment_AppearInPrepOnly()
        {
            var service = CreateService(new FakeUnitOfWork());
            var departments = new List<DepartmentVm>
            {
                Vm(5, "Operations", "OPS", DepartmentKind.Administrative),
                Vm(6, "Store", "STORE", DepartmentKind.Room, DepartmentSection.SeniorSchool, 5),
                Vm(7, "Prep Library", "PLIB", DepartmentKind.Room, DepartmentSection.Prep, 5)
            };

            var sections = service.GetTreeSections(departments).ToList();

            Assert.AreEqual(2, sections.Count);
            Assert.AreEqual(DepartmentSection.Prep, sections[0].SectionGroup);
            Assert.AreEqual(new[] { 7 }, sections[0].Items[0].Children.Select(x => x.Id).ToArray());
            Assert.AreEqual(DepartmentSection.SeniorSchool, sections[1].SectionGroup);
            Assert.AreEqual(new[] { 6 }, sections[1].Items[0].Children.Select(x => x.Id).ToArray());
            Assert.AreEqual(5, sections[0].Items[0].Id);
            Assert.AreEqual(5, sections[1].Items[0].Id);
        }

        [Test]
        public void TreeSections_IndependentPrepRoom_GoesToThePrepOtherCard()
        {
            var service = CreateService(new FakeUnitOfWork());
            var departments = new List<DepartmentVm>
            {
                Vm(7, "Prep Field", "PFIELD", DepartmentKind.Room, DepartmentSection.Prep),
                Vm(8, "Sports Field", "SFIELD", DepartmentKind.Room)
            };

            var sections = service.GetTreeSections(departments).ToList();

            Assert.AreEqual(2, sections.Count);
            Assert.AreEqual("Other", sections[0].Title);
            Assert.AreEqual(DepartmentSection.Prep, sections[0].SectionGroup);
            Assert.AreEqual(new[] { 7 }, sections[0].Items.Select(x => x.Id).ToArray());
            Assert.AreEqual(new[] { 8 }, sections[1].Items.Select(x => x.Id).ToArray());
        }

        [Test]
        public void BuildTreeOptions_PrepRootsFirst_GradesBySortNumber()
        {
            var options = DepartmentTreeHelper.BuildTreeOptions(new List<DepartmentVm>
            {
                Vm(1, "Grade 10", "G10", DepartmentKind.Grade),
                Vm(2, "Grade 2", "G02", DepartmentKind.Grade),
                Vm(3, "Prep", "G00", DepartmentKind.Grade, DepartmentSection.Prep)
            }, false);

            Assert.AreEqual(new[] { 3, 2, 1 }, options.Select(x => x.Key).ToArray());
        }

        // ---- Create Grade / Create Class ----

        [Test]
        public void CreateGrade_CreatesPrepGradeG00_WithSectionAndSortNumber()
        {
            var unitOfWork = new FakeUnitOfWork();
            var audit = new CapturingAuditWriter();
            var service = CreateService(unitOfWork, audit);

            var id = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.Grade,
                Name = "Prep",
                Code = "g00",
                Section = DepartmentSection.Prep,
                SortOrder = 0
            });

            var created = unitOfWork.Repository<Department>().GetById(id);
            Assert.AreEqual(DepartmentKind.Grade, created.DepartmentKind);
            Assert.AreEqual("G00", created.Code);
            Assert.AreEqual("Prep", created.Name);
            Assert.AreEqual(DepartmentSection.Prep, created.Section);
            Assert.AreEqual(0, created.SortOrder);
            Assert.IsTrue(created.IsActive);
            Assert.IsFalse(created.ParentDepartmentId.HasValue);
            Assert.IsTrue(audit.Entries.Any(x => x[0] == "Departments.Create" && x[3].Contains("Section=Prep")));
        }

        [Test]
        public void CreateGrade_DefaultsToSeniorSchool()
        {
            var unitOfWork = new FakeUnitOfWork();
            var id = CreateService(unitOfWork).CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.Grade,
                Name = "Form 1",
                Code = "FORM1"
            });

            Assert.AreEqual(DepartmentSection.SeniorSchool, unitOfWork.Repository<Department>().GetById(id).Section);
        }

        [Test]
        public void CreateGrade_RejectsMissingNameOrCode_DuplicateCode_DuplicateName_AndBadSortNumber()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 1, Name = "Grade 1", Code = "G01", DepartmentKind = DepartmentKind.Grade, IsActive = true });
            var service = CreateService(unitOfWork);

            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Grade, Name = "", Code = "G05" }));
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Grade, Name = "Grade 5", Code = "" }));
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Grade, Name = "Other", Code = "g01" }), "duplicate code");
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Grade, Name = "grade 1", Code = "G09" }), "duplicate grade name");
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Grade, Name = "Grade 6", Code = "G06", SortOrder = 1000 }));
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Grade, Name = "Grade 6", Code = "G06", SortOrder = -1 }));
            Assert.AreEqual(1, unitOfWork.Repository<Department>().GetAll().Count(), "nothing was created by the rejected calls");
        }

        [Test]
        public void CreateClass_TakesTheGradesSection_AndGeneratesACode()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 2, Name = "Prep", Code = "G00", DepartmentKind = DepartmentKind.Grade, Section = DepartmentSection.Prep, IsActive = true });
            var service = CreateService(unitOfWork);

            var id = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.Class,
                ParentDepartmentId = 2,
                Name = "Red"
            });

            var created = unitOfWork.Repository<Department>().GetById(id);
            Assert.AreEqual(DepartmentKind.Class, created.DepartmentKind);
            Assert.AreEqual(2, created.ParentDepartmentId);
            Assert.AreEqual(DepartmentSection.Prep, created.Section);
            Assert.AreEqual("G00RED", created.Code);
            Assert.IsTrue(created.IsActive);
        }

        [Test]
        public void CreateClass_MakesTheCodeUnique_AndRejectsDuplicateNameUnderTheSameGrade()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 1, Name = "Grade 3", Code = "G03", DepartmentKind = DepartmentKind.Grade, IsActive = true });
            unitOfWork.Seed(new Department { Id = 10, Name = "3C", Code = "G03C", ParentDepartmentId = 1, DepartmentKind = DepartmentKind.Class, IsActive = true });
            var service = CreateService(unitOfWork);

            var id = service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Class, ParentDepartmentId = 1, Name = "C!" });
            var code = unitOfWork.Repository<Department>().GetById(id).Code;
            Assert.AreNotEqual("G03C", code);
            Assert.IsTrue(code.StartsWith("G03C"));
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Class, ParentDepartmentId = 1, Name = "3c" }));
        }

        [Test]
        public void CreateClass_RequiresAnActiveGrade()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 1, Name = "Old", Code = "G09", DepartmentKind = DepartmentKind.Grade, IsActive = false });
            unitOfWork.Seed(new Department { Id = 5, Name = "Finance", Code = "FIN", DepartmentKind = DepartmentKind.Administrative, IsActive = true });
            var service = CreateService(unitOfWork);

            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Class, Name = "A" }), "no grade chosen");
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Class, ParentDepartmentId = 1, Name = "A" }), "inactive grade");
            Assert.Throws<BusinessException>(() => service.CreateFromWizard(new DepartmentCreateVm { SetupMode = DepartmentSetupModes.Class, ParentDepartmentId = 5, Name = "A" }), "not a grade");
        }

        [Test]
        public void BuildSingleClassCode_GradeCodePlusLettersAndDigits_CappedAtTwenty()
        {
            Assert.AreEqual("G03C", DepartmentService.BuildSingleClassCode("G03", "C"));
            Assert.AreEqual("G03C", DepartmentService.BuildSingleClassCode("G03", "3C"), "leading grade number is not repeated");
            Assert.AreEqual("G00RED", DepartmentService.BuildSingleClassCode("G00", "Red"));
            Assert.AreEqual(20, DepartmentService.BuildSingleClassCode("ABCDEFGHIJKLMNOP", "Very long class name").Length);
        }

        // ---- Section flows through the other create paths ----

        [Test]
        public void CreateNormalDepartment_AndIndependentRoom_UseTheChosenSection()
        {
            var unitOfWork = new FakeUnitOfWork();
            var service = CreateService(unitOfWork);

            var roomId = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.Room,
                Name = "Prep Gala",
                Code = "GALA",
                Section = DepartmentSection.Prep
            });
            var deptId = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.Normal,
                Name = "Prep Office",
                Code = "POFF"
            });

            Assert.AreEqual(DepartmentSection.Prep, unitOfWork.Repository<Department>().GetById(roomId).Section);
            Assert.AreEqual(DepartmentSection.SeniorSchool, unitOfWork.Repository<Department>().GetById(deptId).Section, "default");
        }

        [Test]
        public void CreateRoomAndSubDepartment_InheritTheParentsSection_WhenNoneChosen()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 5, Name = "Prep Admin", Code = "PADM", DepartmentKind = DepartmentKind.Administrative, Section = DepartmentSection.Prep, IsActive = true });
            var service = CreateService(unitOfWork);

            var subId = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.SubDepartment,
                ParentDepartmentId = 5,
                LockedParentDepartmentId = 5,
                Name = "Resources"
            });
            var roomId = service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.Room,
                ParentDepartmentId = 5,
                LockedParentDepartmentId = 5,
                Name = "Store"
            });

            Assert.AreEqual(DepartmentSection.Prep, unitOfWork.Repository<Department>().GetById(subId).Section);
            Assert.AreEqual(DepartmentSection.Prep, unitOfWork.Repository<Department>().GetById(roomId).Section);
        }

        [Test]
        public void GradeWithStreamsWizard_UsesTheChosenSectionForGradeAndClasses()
        {
            var unitOfWork = new FakeUnitOfWork();
            var service = CreateService(unitOfWork);

            service.CreateFromWizard(new DepartmentCreateVm
            {
                SetupMode = DepartmentSetupModes.GradeStreams,
                GradeNumber = 1,
                SelectedStreams = "A,B",
                Section = DepartmentSection.Prep
            });

            var all = unitOfWork.Repository<Department>().GetAll().ToList();
            Assert.IsTrue(all.Count >= 3);
            Assert.IsTrue(all.All(x => x.Section == DepartmentSection.Prep));
        }

        // ---- Update ----

        [Test]
        public void Update_ChangingAGradesSection_MovesItsClassesThatWereInTheOldSection_AndAudits()
        {
            var unitOfWork = new FakeUnitOfWork();
            var audit = new CapturingAuditWriter();
            unitOfWork.Seed(new Department { Id = 2, Name = "Prep", Code = "G00", DepartmentKind = DepartmentKind.Grade, IsActive = true });
            unitOfWork.Seed(new Department { Id = 20, Name = "Red", Code = "G00R", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Class, IsActive = true });
            unitOfWork.Seed(new Department { Id = 21, Name = "Blue", Code = "G00B", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Class, IsActive = true, Section = DepartmentSection.Prep });
            var service = CreateService(unitOfWork, audit);

            service.Update(new DepartmentVm
            {
                Id = 2,
                Name = "Prep",
                Code = "G00",
                DepartmentKind = DepartmentKind.Grade,
                IsActive = true,
                Section = DepartmentSection.Prep,
                SortOrder = 0,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            });

            var repo = unitOfWork.Repository<Department>();
            Assert.AreEqual(DepartmentSection.Prep, repo.GetById(2).Section);
            Assert.AreEqual(0, repo.GetById(2).SortOrder);
            Assert.AreEqual(DepartmentSection.Prep, repo.GetById(20).Section, "class that was on the old Section follows the grade");
            Assert.AreEqual(DepartmentSection.Prep, repo.GetById(21).Section);
            Assert.IsTrue(audit.Entries.Any(x => x[1] == "2" && (x[2] ?? string.Empty).Contains("Section=SeniorSchool") && (x[3] ?? string.Empty).Contains("Section=Prep")), "grade audit shows old and new Section");
            Assert.IsTrue(audit.Entries.Any(x => x[1] == "20" && (x[3] ?? string.Empty).Contains("Section=Prep")), "follower class is audited too");
        }

        [Test]
        public void Update_ClassThatWasSetToADifferentSection_IsNotDraggedAlongByTheGrade()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 2, Name = "Grade 1", Code = "G01", DepartmentKind = DepartmentKind.Grade, IsActive = true, Section = DepartmentSection.Prep });
            unitOfWork.Seed(new Department { Id = 20, Name = "Odd", Code = "G01O", ParentDepartmentId = 2, DepartmentKind = DepartmentKind.Class, IsActive = true, Section = DepartmentSection.SeniorSchool });
            var service = CreateService(unitOfWork);

            service.Update(new DepartmentVm
            {
                Id = 2,
                Name = "Grade 1",
                Code = "G01",
                DepartmentKind = DepartmentKind.Grade,
                IsActive = true,
                Section = DepartmentSection.SeniorSchool,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            });

            var repo = unitOfWork.Repository<Department>();
            Assert.AreEqual(DepartmentSection.SeniorSchool, repo.GetById(2).Section);
            Assert.AreEqual(DepartmentSection.SeniorSchool, repo.GetById(20).Section);
        }

        [Test]
        public void Update_WithoutSection_LeavesTheStoredSectionAlone()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 7, Name = "Prep Field", Code = "PFIELD", DepartmentKind = DepartmentKind.Room, IsActive = true, Section = DepartmentSection.Prep, IsRequisitionTarget = true });
            var service = CreateService(unitOfWork);

            service.Update(new DepartmentVm
            {
                Id = 7,
                Name = "Prep Field 2",
                Code = "PFIELD",
                DepartmentKind = DepartmentKind.Room,
                IsActive = true,
                IsRequisitionTarget = true,
                Section = null,
                RequisitionFlowMode = RequisitionFlowMode.InheritParent
            });

            Assert.AreEqual(DepartmentSection.Prep, unitOfWork.Repository<Department>().GetById(7).Section);
            Assert.AreEqual("Prep Field 2", unitOfWork.Repository<Department>().GetById(7).Name);
        }

        [Test]
        public void MapDepartment_CarriesSectionAndSortOrderToTheViewModel()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new Department { Id = 2, Name = "Prep", Code = "G00", DepartmentKind = DepartmentKind.Grade, IsActive = true, Section = DepartmentSection.Prep, SortOrder = 0 });
            var vm = CreateService(unitOfWork).GetById(2);

            Assert.AreEqual(DepartmentSection.Prep, vm.Section);
            Assert.AreEqual(0, vm.SortOrder);
        }
    }
}
