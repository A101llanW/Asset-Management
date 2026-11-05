using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Approvals
{
    /// <summary>Migration 112: company section heads (KG / Prep / Senior) after the department-tree walk, before role holders.</summary>
    [TestFixture]
    public class OrganizationSectionHeadsTests
    {
        private const int Hod = 8;
        private const int Staff = 3;

        private static FakeUnitOfWork SeedDepts()
        {
            var uow = new FakeUnitOfWork();
            uow.Seed(new Department { Id = 40, Name = "Academics", DepartmentKind = DepartmentKind.Administrative, IsActive = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 41, Name = "Prep Room", ParentDepartmentId = 40, DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 42, Name = "Senior Lab", ParentDepartmentId = 40, DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.SeniorSchool, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 60, Name = "KG Room", ParentDepartmentId = 40, DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.KG, IsActive = true, IsRequisitionTarget = true, OrganizationId = 1 });
            uow.Seed(new Department { Id = 140, Name = "Other Org Academics", DepartmentKind = DepartmentKind.Administrative, IsActive = true, OrganizationId = 2 });
            uow.Seed(new Department { Id = 141, Name = "Other Prep Room", ParentDepartmentId = 140, DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep, IsActive = true, OrganizationId = 2 });
            uow.Seed(new Role { Id = Hod, Name = "Department Head", IsActive = true, OrganizationId = 1 });
            uow.Seed(new Role { Id = Staff, Name = "Staff", IsActive = true, OrganizationId = 1 });
            return uow;
        }

        private static FakeUserService Users()
        {
            var users = new FakeUserService();
            users.Seed(new UserVm { Id = "org-prep", RoleId = Staff, DepartmentId = 40, IsActive = true });
            users.Seed(new UserVm { Id = "org-senior", RoleId = Staff, DepartmentId = 40, IsActive = true });
            users.Seed(new UserVm { Id = "org-kg", RoleId = Staff, DepartmentId = 40, IsActive = true });
            users.Seed(new UserVm { Id = "dept-prep", RoleId = Staff, DepartmentId = 40, IsActive = true });
            users.Seed(new UserVm { Id = "hod-acad", RoleId = Hod, DepartmentId = 40, IsActive = true });
            users.Seed(new UserVm { Id = "other-org-prep", RoleId = Staff, DepartmentId = 140, IsActive = true });
            return users;
        }

        private static Func<DepartmentSection, string> OrgMap(params string[] triples)
        {
            var map = new Dictionary<DepartmentSection, string>();
            if (triples != null)
            {
                foreach (var t in triples)
                {
                    if (string.IsNullOrWhiteSpace(t))
                    {
                        continue;
                    }

                    var parts = t.Split('=');
                    DepartmentSection section;
                    if (parts.Length == 2 && Enum.TryParse(parts[0], true, out section))
                    {
                        map[DepartmentSectionHelper.Normalize(section)] = parts[1];
                    }
                }
            }

            return s => OrganizationSectionHeads.Get(map, s);
        }

        private static StageApproverScope Resolve(FakeUnitOfWork uow, FakeUserService users, int dept, Func<DepartmentSection, string> orgHeads)
        {
            var resolver = new StageApproverScopeResolver(
                () => users.GetAll(),
                id => uow.Repository<Department>().GetById(id),
                id =>
                {
                    var role = uow.Repository<Role>().GetById(id);
                    return role == null ? null : role.Name;
                },
                orgHeads);
            return resolver.Resolve(Hod, null, dept);
        }

        [Test]
        public void PrepRequest_UsesCompanyPrepHead_WhenNoDepartmentHead()
        {
            var s = Resolve(SeedDepts(), Users(), 41, OrgMap("Prep=org-prep"));
            Assert.AreEqual(StageApproverScopeKind.SectionHead, s.Kind);
            Assert.IsNull(s.ResolvedDepartmentId);
            CollectionAssert.AreEqual(new[] { "org-prep" }, s.UserIds);
            Assert.IsTrue(s.BypassesRoleCheck);
            Assert.IsFalse(s.Contains("hod-acad"));
        }

        [Test]
        public void SeniorAndKg_UseTheirOwnCompanyHeads()
        {
            var org = OrgMap("Prep=org-prep", "SeniorSchool=org-senior", "KG=org-kg");
            CollectionAssert.AreEqual(new[] { "org-senior" }, Resolve(SeedDepts(), Users(), 42, org).UserIds);
            CollectionAssert.AreEqual(new[] { "org-kg" }, Resolve(SeedDepts(), Users(), 60, org).UserIds);
        }

        [Test]
        public void DepartmentSectionHead_WinsOverCompanyHead()
        {
            var uow = SeedDepts();
            uow.Repository<Department>().GetById(40).PrepHeadUserId = "dept-prep";
            var s = Resolve(uow, Users(), 41, OrgMap("Prep=org-prep"));
            CollectionAssert.AreEqual(new[] { "dept-prep" }, s.UserIds);
            Assert.AreEqual(40, s.ResolvedDepartmentId);
        }

        [Test]
        public void NoCompanyHead_FallsBackToRoleHolders()
        {
            var s = Resolve(SeedDepts(), Users(), 41, OrgMap());
            Assert.AreEqual(StageApproverScopeKind.AncestorDepartment, s.Kind);
            CollectionAssert.AreEqual(new[] { "hod-acad" }, s.UserIds);
        }

        [Test]
        public void InactiveCompanyHead_IsIgnored()
        {
            var users = Users();
            users.SetActive("org-prep", false);
            var s = Resolve(SeedDepts(), users, 41, OrgMap("Prep=org-prep"));
            CollectionAssert.AreEqual(new[] { "hod-acad" }, s.UserIds);
        }

        [Test]
        public void Resolve_Tree_ThenOrg_ReportsIsOrganizationHead()
        {
            Func<int, Department> get = id => SeedDepts().Repository<Department>().GetById(id);
            var match = DepartmentSectionHeads.Resolve(41, DepartmentSection.Prep, get, id => true, OrgMap("Prep=org-prep"));
            Assert.IsNotNull(match);
            Assert.IsTrue(match.IsOrganizationHead);
            Assert.AreEqual(0, match.DepartmentId);
            Assert.AreEqual("org-prep", match.UserId);
        }

        [Test]
        public void Display_ShowsCompanySectionHead_WhenTreeEmpty()
        {
            var vms = new Dictionary<int, DepartmentVm>
            {
                { 41, new DepartmentVm { Id = 41, Name = "Prep Room", DepartmentKind = DepartmentKind.Room, Section = DepartmentSection.Prep } }
            };
            var rows = DepartmentSectionHeadDisplay.Build(
                vms[41],
                id => null,
                id => id == "org-prep" ? "Org Prep" : id,
                id => true,
                OrgMap("Prep=org-prep"));
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Org Prep", rows[0].HeadName);
            Assert.AreEqual(OrganizationSectionHeads.OrgFallbackSource, rows[0].Source);
            Assert.IsFalse(rows[0].IsFallback);
        }

        [Test]
        public void ToMap_Get_EnsureRows_Apply_AreTenantScopedAndIdempotent()
        {
            var existing = new List<OrganizationSectionHead>
            {
                new OrganizationSectionHead { Id = 1, OrganizationId = 1, Section = DepartmentSection.Prep, HeadUserId = "a" },
                new OrganizationSectionHead { Id = 2, OrganizationId = 2, Section = DepartmentSection.Prep, HeadUserId = "other" }
            };
            var added = new List<OrganizationSectionHead>();
            var org1Only = existing.Where(x => x.OrganizationId == 1).ToList();
            var rows = OrganizationSectionHeads.EnsureRows(org1Only, 1, added.Add);
            Assert.AreEqual(3, rows.Count);
            Assert.AreEqual(2, added.Count);
            Assert.AreEqual("a", OrganizationSectionHeads.Get(OrganizationSectionHeads.ToMap(rows), DepartmentSection.Prep));

            OrganizationSectionHeads.Apply(rows, "kg1", "prep1", "senior1");
            Assert.AreEqual("kg1", rows.Single(x => x.Section == DepartmentSection.KG).HeadUserId);
            Assert.AreEqual("prep1", rows.Single(x => x.Section == DepartmentSection.Prep).HeadUserId);
            Assert.AreEqual("senior1", rows.Single(x => x.Section == DepartmentSection.SeniorSchool).HeadUserId);

            // Other tenant row is untouched by Apply on org 1.
            Assert.AreEqual("other", existing.Single(x => x.OrganizationId == 2).HeadUserId);

            OrganizationSectionHeads.Apply(rows, " ", null, "");
            Assert.IsNull(rows.Single(x => x.Section == DepartmentSection.KG).HeadUserId);
            Assert.IsNull(rows.Single(x => x.Section == DepartmentSection.Prep).HeadUserId);
            Assert.IsNull(rows.Single(x => x.Section == DepartmentSection.SeniorSchool).HeadUserId);
            Assert.AreEqual(1, org1Only.Count);
            Assert.AreEqual("other", existing.Single(x => x.OrganizationId == 2).HeadUserId);
        }

        [Test]
        public void SoftBackfillExpectation_DocumentedInMigrationSql()
        {
            var directory = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            string scripts = null;
            while (directory != null)
            {
                var candidate = System.IO.Path.Combine(directory.FullName, "database", "scripts");
                if (System.IO.Directory.Exists(candidate))
                {
                    scripts = candidate;
                    break;
                }

                directory = directory.Parent;
            }

            if (scripts == null)
            {
                Assert.Inconclusive("database/scripts not found.");
            }

            var sql = System.IO.File.ReadAllText(System.IO.Path.Combine(scripts, "004_Migrations", "112_OrganizationSectionHead.sql"));
            StringAssert.Contains("DistinctCount] = 1", sql);
            StringAssert.Contains("PrepHeadUserId", sql);
            StringAssert.Contains("SeniorHeadUserId", sql);
            StringAssert.Contains("KgHeadUserId", sql);
        }
    }
}