using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Approvals
{
    /// <summary>
    /// Migration 107: a department can have a different head for Prep and for Senior school, whatever their role. The
    /// Department Head stage goes to the section head of the request's unit (or nearest ancestor); none set = old rule.
    /// </summary>
    [TestFixture]
    public class DepartmentSectionHeadsTests
    {
        private const int Hod = 8;
        private const int ProcOfficer = 9;
        private const int Staff = 3;

        // Academics(40, heads: Prep=prep-head, Senior=senior-head) -> Prep Room(41, Prep) / Senior Lab(42, Senior)
        // Year 3 (43, Prep grade, no heads) ; Science(50, no heads)
        private static FakeUnitOfWork Seed(string prepHead = "prep-head", string seniorHead = "senior-head")
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department { Id = 40, Name = "Academics", DepartmentKind = DepartmentKind.Administrative, IsActive = true, OrganizationId = 1, PrepHeadUserId = prepHead, SeniorHeadUserId = seniorHead });
            uow.Seed(new Department { Id = 41, Name = "Prep Room", ParentDepartmentId = 40, DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 42, Name = "Senior Lab", ParentDepartmentId = 40, DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.SeniorSchool, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 43, Name = "Year 3", Code = "G03", DepartmentKind = DepartmentKind.Grade, Section = DepartmentSection.Prep, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 50, Name = "Science", DepartmentKind = DepartmentKind.Administrative, IsActive = true, OrganizationId = 1 });
            uow.Seed(new Role { Id = Hod, Name = "Department Head", IsActive = true, OrganizationId = 1 });
            uow.Seed(new Role { Id = ProcOfficer, Name = "Procurement Officer", IsActive = true, OrganizationId = 1 });
            uow.Seed(new Role { Id = Staff, Name = "Staff", IsActive = true, OrganizationId = 1 });
            return uow;
        }

        private static FakeUserService Users(bool prepHeadActive = true, bool hodOnPrepRoom = false)
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "prep-head", RoleId = Staff, DepartmentId = 41, IsActive = prepHeadActive });
            users.Seed(new UserVm { Id = "senior-head", RoleId = ProcOfficer, DepartmentId = 50, IsActive = true });
            users.Seed(new UserVm { Id = "hod-acad", RoleId = Hod, DepartmentId = 40, IsActive = true });
            users.Seed(new UserVm { Id = "hod-sci", RoleId = Hod, DepartmentId = 50, IsActive = true });
            users.Seed(new UserVm { Id = "proc-acad", RoleId = ProcOfficer, DepartmentId = 40, IsActive = true });
            if (hodOnPrepRoom)
            {
                users.Seed(new UserVm { Id = "hod-room", RoleId = Hod, DepartmentId = 41, IsActive = true });
            }

            return users;
        }

        private static StageApproverScope Resolve(FakeUnitOfWork uow, FakeUserService users, int role, int dept, string person = null, bool catalog = false)
        {
            return StageApproverScopeResolver.ResolveFor(uow, users, role, person, dept, catalog);
        }

        [Test]
        public void PrepRequest_GoesToThePrepHead_NotTheRoleHolders()
        {
            var s = Resolve(Seed(), Users(), Hod, 41);
            Assert.AreEqual(StageApproverScopeKind.SectionHead, s.Kind);
            Assert.AreEqual(40, s.ResolvedDepartmentId);
            CollectionAssert.AreEqual(new[] { "prep-head" }, s.UserIds);
            Assert.IsFalse(s.Contains("hod-acad"));
            Assert.IsTrue(s.BypassesRoleCheck);
        }

        [Test]
        public void SeniorRequest_SameDepartment_GoesToTheOtherPerson()
        {
            var s = Resolve(Seed(), Users(), Hod, 42);
            Assert.AreEqual(StageApproverScopeKind.SectionHead, s.Kind);
            CollectionAssert.AreEqual(new[] { "senior-head" }, s.UserIds);
        }

        [Test]
        public void RequestOnTheDepartmentItself_UsesItsOwnSection()
        {
            // Academics is Senior school (default) -> senior head.
            CollectionAssert.AreEqual(new[] { "senior-head" }, Resolve(Seed(), Users(), Hod, 40).UserIds);
        }

        [Test]
        public void SectionHeadNotSet_FallsBackToTheDepartmentHeadRoleHolders()
        {
            var s = Resolve(Seed(prepHead: null), Users(), Hod, 41);
            Assert.AreEqual(StageApproverScopeKind.AncestorDepartment, s.Kind);
            CollectionAssert.AreEqual(new[] { "hod-acad" }, s.UserIds);
            Assert.IsFalse(s.BypassesRoleCheck);
        }

        [Test]
        public void InactiveSectionHead_IsIgnored_FallsBack()
        {
            var s = Resolve(Seed(), Users(prepHeadActive: false), Hod, 41);
            CollectionAssert.AreEqual(new[] { "hod-acad" }, s.UserIds);
        }

        [Test]
        public void NoHeadsAnywhere_OldBehaviourUnchanged()
        {
            var s = Resolve(Seed(), Users(), Hod, 50);
            Assert.AreEqual(StageApproverScopeKind.Department, s.Kind);
            CollectionAssert.AreEqual(new[] { "hod-sci" }, s.UserIds);
        }

        [Test]
        public void OtherStageRoles_AreNotAffected()
        {
            var s = Resolve(Seed(), Users(), ProcOfficer, 41);
            Assert.AreEqual(StageApproverScopeKind.AncestorDepartment, s.Kind);
            CollectionAssert.AreEqual(new[] { "proc-acad" }, s.UserIds);
        }

        [Test]
        public void NamedPersonOnTheStage_StillWins()
        {
            var s = Resolve(Seed(), Users(), Hod, 41, "hod-sci");
            Assert.AreEqual(StageApproverScopeKind.NamedPerson, s.Kind);
        }

        [Test]
        public void CatalogPathFlow_DepartmentHeadStage_StillUsesTheSectionHead()
        {
            var s = Resolve(Seed(), Users(), Hod, 41, null, true);
            Assert.AreEqual(StageApproverScopeKind.SectionHead, s.Kind);
            CollectionAssert.AreEqual(new[] { "prep-head" }, s.UserIds);
        }

        [Test]
        public void RoleHolderOnANearerUnit_StillWins()
        {
            var s = Resolve(Seed(), Users(hodOnPrepRoom: true), Hod, 41);
            Assert.AreEqual(StageApproverScopeKind.Department, s.Kind);
            CollectionAssert.AreEqual(new[] { "hod-room" }, s.UserIds);
        }

        [Test]
        public void SectionHead_CanApprove_WithoutHoldingTheRole_OthersCannot()
        {
            var uow = Seed();
            var scope = Resolve(uow, Users(), Hod, 41);
            Assert.DoesNotThrow(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                uow, null, Staff, false, Hod, null, "requester", "prep-head", ApprovalProcessCodes.Purchase, scope));
            Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                uow, null, Hod, false, Hod, null, "requester", "hod-acad", ApprovalProcessCodes.Purchase, scope));
            Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                uow, null, ProcOfficer, false, Hod, null, "requester", "senior-head", ApprovalProcessCodes.Purchase, scope));

            Assert.IsTrue(ApprovalWorkflowHelper.CanUserActOnStage(uow, "requester", "prep-head", false, Staff, Hod, null, scope));
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage(uow, "requester", "hod-acad", false, Hod, Hod, null, scope));
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage(uow, "requester", "senior-head", false, ProcOfficer, Hod, null, scope));
        }

        [Test]
        public void SectionHead_CannotApproveTheirOwnRequest()
        {
            var uow = Seed();
            var scope = Resolve(uow, Users(), Hod, 41);
            Assert.Throws<BusinessException>(() => ApprovalWorkflowHelper.EnsureUserCanApprove(
                uow, null, Staff, false, Hod, null, "prep-head", "prep-head", ApprovalProcessCodes.Purchase, scope));
            Assert.IsFalse(ApprovalWorkflowHelper.CanUserActOnStage(uow, "prep-head", "prep-head", false, Staff, Hod, null, scope));
        }

        [TestCase("Department Head", true)]
        [TestCase("department head", true)]
        [TestCase("HOD", true)]
        [TestCase("Head of Department", true)]
        [TestCase("Procurement Officer", false)]
        [TestCase(null, false)]
        public void DepartmentHeadRoleNames(string name, bool expected)
        {
            Assert.AreEqual(expected, DepartmentHeadAssignmentRules.IsDepartmentHeadRoleName(name));
        }

        [Test]
        public void Display_ShowsBothSectionsForADepartment_AndTheInheritedHeadForARoom()
        {
            var vms = new Dictionary<int, DepartmentVm>
            {
                { 40, new DepartmentVm { Id = 40, Name = "Academics", DepartmentKind = DepartmentKind.Administrative, PrepHeadUserId = "prep-head" } },
                { 41, new DepartmentVm { Id = 41, Name = "Prep Room", ParentDepartmentId = 40, DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep } }
            };
            Func<int, DepartmentVm> get = id => vms.ContainsKey(id) ? vms[id] : null;
            Func<string, string> label = id => id == "prep-head" ? "Pat Prep" : null;

            var dept = DepartmentSectionHeadDisplay.Build(vms[40], get, label, id => true);
            Assert.AreEqual(2, dept.Count);
            Assert.AreEqual("Pat Prep", dept[0].HeadName);
            Assert.AreEqual("Set on this unit", dept[0].Source);
            Assert.IsTrue(dept[1].IsFallback);

            var room = DepartmentSectionHeadDisplay.Build(vms[41], get, label, id => true);
            Assert.AreEqual(1, room.Count);
            Assert.AreEqual("Pat Prep", room[0].HeadName);
            Assert.AreEqual("From Academics", room[0].Source);
        }

        [Test]
        public void ClassroomsAndRooms_CannotCarryHeads()
        {
            Assert.IsFalse(DepartmentSectionHeads.CanHaveSectionHeads(DepartmentKind.Class));
            Assert.IsFalse(DepartmentSectionHeads.CanHaveSectionHeads(DepartmentKind.Room));
            Assert.IsTrue(DepartmentSectionHeads.CanHaveSectionHeads(DepartmentKind.Administrative));
            Assert.IsTrue(DepartmentSectionHeads.CanHaveSectionHeads(DepartmentKind.SubDepartment));
            Assert.IsTrue(DepartmentSectionHeads.CanHaveSectionHeads(DepartmentKind.Grade));
        }

        [Test]
        public void IndexOwnerColumn_ShowsTheSectionHeads()
        {
            var vm = new DepartmentVm { DepartmentKind = DepartmentKind.Administrative, PrepHeadName = "Pat", SeniorHeadUserId = "u2", SeniorHeadName = "Sam" };
            Assert.AreEqual("Pat (Head, Prep); Sam (Head, Senior school)", DepartmentPicDisplay.FormatRequisitionOwner(vm));
            Assert.AreEqual(string.Empty, DepartmentPicDisplay.FormatRequisitionOwner(new DepartmentVm { DepartmentKind = DepartmentKind.Grade }));
        }

        [Test]
        public void Migration107_IsAdditiveGuardedAndHasARollback()
        {
            var dir = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "database", "scripts")))
            {
                dir = dir.Parent;
            }

            if (dir == null)
            {
                Assert.Inconclusive("database/scripts not found.");
            }

            var scripts = System.IO.Path.Combine(dir.FullName, "database", "scripts");
            Assert.AreEqual(1, System.IO.Directory.GetFiles(System.IO.Path.Combine(scripts, "004_Migrations"), "107_*.sql").Length);
            var sql = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "004_Migrations", "107_DepartmentSectionHeads.sql"));
            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'PrepHeadUserId') IS NULL", sql);
            StringAssert.Contains("COL_LENGTH(N'dbo.Department', N'SeniorHeadUserId') IS NULL", sql);
            StringAssert.Contains("FK_Department_PrepHeadUser", sql);
            StringAssert.Contains("FK_Department_SeniorHeadUser", sql);
            StringAssert.Contains("IF NOT EXISTS (SELECT 1 FROM [dbo].[SchemaMigrationHistory] WHERE [ScriptName] = N'107_DepartmentSectionHeads.sql')", sql);
            var executable = string.Join("\n", sql.Split('\n').Where(x => !x.TrimStart().StartsWith("--", StringComparison.Ordinal)));
            StringAssert.DoesNotContain("DROP ", executable.ToUpperInvariant());
            StringAssert.DoesNotContain("UPDATE ", executable.ToUpperInvariant());
            StringAssert.DoesNotContain("ASSETTAG", executable.ToUpperInvariant());
            Assert.IsTrue(System.IO.File.Exists(System.IO.Path.Combine(scripts, "ops", "107_Rollback_DepartmentSectionHeads_PREPARE_DO_NOT_RUN.sql")));
        }
    }
}
