using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// OrgReferenceValidator: reject (interactive) vs drop (bulk/import/stored) behaviour and audit logging.
    /// Org 13 = Test-WF, org 11 = 'Template 3' (owner of role ids 45/41 in the 16 Sep incident).
    /// </summary>
    [TestFixture]
    public class OrgReferenceValidatorTests
    {
        private FakeUnitOfWork _unitOfWork;
        private RecordingAuditWriter _audit;
        private StubReferenceDataCache _cache;

        [SetUp]
        public void SetUp()
        {
            _unitOfWork = new FakeUnitOfWork();
            _audit = new RecordingAuditWriter();
            _cache = new StubReferenceDataCache();

            Seed(new Role { Id = 41, Name = "Company Admin", OrganizationId = 11, IsActive = true });
            Seed(new Role { Id = 45, Name = "Department Head", OrganizationId = 11, IsActive = true });
            Seed(new Role { Id = 130, Name = "Company Admin", OrganizationId = 13, IsActive = true });
            Seed(new Role { Id = 131, Name = "Department Head", OrganizationId = 13, IsActive = true });
            Seed(new Role { Id = 139, Name = "Retired Role", OrganizationId = 13, IsActive = false });
            _cache.Roles[13] = new List<RoleVm> { new RoleVm { Id = 130, Name = "Company Admin" }, new RoleVm { Id = 131, Name = "Department Head" } };
            _cache.Users[13] = new List<UserVm> { new UserVm { Id = "u13-a" } };
            _cache.Users[11] = new List<UserVm> { new UserVm { Id = "u11-x" } };

            Seed(new Department { Id = 5, Name = "WF Dept", OrganizationId = 13, IsActive = true });
            Seed(new Department { Id = 6, Name = "Template Dept", OrganizationId = 11, IsActive = true });
            Seed(new AssetCategory { Id = 7, Name = "WF Cat", OrganizationId = 13, IsActive = true });
            Seed(new AssetCategory { Id = 8, Name = "Template Cat", OrganizationId = 11, IsActive = true });
        }

        private void Seed<T>(T entity) where T : class, new()
        {
            _unitOfWork.Seed(entity);
        }

        private OrgReferenceValidator NewValidator()
        {
            return new OrgReferenceValidator(_unitOfWork, _audit, _cache);
        }

        [Test]
        public void RoleInOrganization_OwnActiveAndInactiveRoles_True_ForeignFalse()
        {
            var validator = NewValidator();

            Assert.IsTrue(validator.RoleInOrganization(13, 130));
            Assert.IsTrue(validator.RoleInOrganization(13, 139), "inactive own role is not cross-org");
            Assert.IsFalse(validator.RoleInOrganization(13, 45));
            Assert.IsFalse(validator.RoleInOrganization(13, 41));
            Assert.IsFalse(validator.RoleInOrganization(13, 9999));
        }

        [Test]
        public void PlatformLevelRows_WithNoOrganization_AreNotAnotherCompany()
        {
            Seed(new Role { Id = 900, Name = "Platform Role", OrganizationId = null, IsActive = true });
            Seed(new Department { Id = 901, Name = "Unscoped", OrganizationId = null, IsActive = true });
            var validator = NewValidator();

            Assert.IsTrue(validator.RoleInOrganization(13, 900));
            Assert.IsTrue(validator.DepartmentInOrganization(13, 901));
            Assert.IsFalse(validator.DepartmentInOrganization(13, 6));
        }

        [Test]
        public void UserInOrganization_UsesOrgFilteredLookup()
        {
            var validator = NewValidator();

            Assert.IsTrue(validator.UserInOrganization(13, "u13-a"));
            Assert.IsFalse(validator.UserInOrganization(13, "u11-x"));
        }

        [Test]
        public void EnsureProcessesInOrganization_ForeignRole_IsRejectedAndAudited()
        {
            var processes = new List<ApprovalProcessSettingsVm>
            {
                new ApprovalProcessSettingsVm
                {
                    ProcessCode = ApprovalProcessCodes.Transfer,
                    DisplayName = "Transfer",
                    RequiresApproval = true,
                    Stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = 45 } }
                }
            };

            var ex = Assert.Throws<BusinessException>(() =>
                NewValidator().EnsureProcessesInOrganization(13, processes, "Asset edit", "Asset", "1001"));

            StringAssert.Contains("role #45", ex.Message);
            Assert.AreEqual(1, _audit.Entries.Count);
            Assert.AreEqual(OrgReferenceGuard.AuditAction, _audit.Entries[0].Action);
            StringAssert.Contains("outcome=rejected", _audit.Entries[0].NewValues);
            StringAssert.Contains("org=13", _audit.Entries[0].OldValues);
        }

        [Test]
        public void EnsureProcessesInOrganization_OwnRolesAndPeople_Pass()
        {
            var processes = new List<ApprovalProcessSettingsVm>
            {
                new ApprovalProcessSettingsVm
                {
                    ProcessCode = ApprovalProcessCodes.Disposal,
                    RequiresApproval = true,
                    Stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = 130, UserId = "u13-a" } }
                }
            };

            NewValidator().EnsureProcessesInOrganization(13, processes, "Asset create", "Asset", null);

            Assert.AreEqual(0, _audit.Entries.Count);
        }

        [Test]
        public void EnsureConfigurationInOrganization_ForeignSnapshotRole_BlocksSubmit()
        {
            var config = new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Transfer,
                DisplayName = "Transfer",
                RequiresApproval = true,
                StageRoleIds = new List<int> { 131, 41 }
            };

            var ex = Assert.Throws<BusinessException>(() =>
                NewValidator().EnsureConfigurationInOrganization(13, config, "Transfer request", "AssetTransfer", "77"));

            StringAssert.Contains("another company", ex.Message);
            StringAssert.Contains("roles 41", ex.Message);
            Assert.AreEqual(1, _audit.Entries.Count);
        }

        [Test]
        public void EnsureConfigurationInOrganization_CheckUsersFalse_IgnoresPersons()
        {
            var config = new ApprovalProcessConfiguration
            {
                ProcessCode = ApprovalProcessCodes.Purchase,
                StageRoleIds = new List<int> { 131 },
                StageUserIds = new List<string> { "u11-x" }
            };

            NewValidator().EnsureConfigurationInOrganization(13, config, "Requisition", "PurchaseRequest", null, checkUsers: false);

            Assert.AreEqual(0, _audit.Entries.Count);
        }

        [Test]
        public void EnsureAssetReferencesInOrganization_ForeignDepartmentAndCategory_Rejected()
        {
            var ex = Assert.Throws<BusinessException>(() =>
                NewValidator().EnsureAssetReferencesInOrganization(13, 6, 8, null, null, null, "Asset create", null));

            StringAssert.Contains("department #6", ex.Message);
            StringAssert.Contains("category #8", ex.Message);
            Assert.AreEqual(1, _audit.Entries.Count);
        }

        [Test]
        public void EnsureAssetReferencesInOrganization_OwnOrEmptyValues_Pass()
        {
            NewValidator().EnsureAssetReferencesInOrganization(13, 5, 7, null, null, null, "Asset create", null);

            Assert.AreEqual(0, _audit.Entries.Count);
        }

        [Test]
        public void EnsureRoleUserDepartment_ForeignIds_Rejected()
        {
            var validator = NewValidator();

            Assert.Throws<BusinessException>(() => validator.EnsureRoleInOrganization(13, 45, "PO raiser role", "Department edit", "Department", "5"));
            Assert.Throws<BusinessException>(() => validator.EnsureUserInOrganization(13, "u11-x", "Class teacher", "Department edit", "Department", "5"));
            Assert.Throws<BusinessException>(() => validator.EnsureDepartmentInOrganization(13, 6, "Parent unit", "Department edit", "Department", "5"));
            validator.EnsureRoleInOrganization(13, null, "PO raiser role", "Department edit", "Department", "5");
            validator.EnsureUserInOrganization(13, "u13-a", "Class teacher", "Department edit", "Department", "5");
            validator.EnsureDepartmentInOrganization(13, 5, "Parent unit", "Department edit", "Department", "5");

            Assert.AreEqual(3, _audit.Entries.Count);
        }

        [Test]
        public void DropForeignStages_ImportDefaults_DropsWarnsAndAudits()
        {
            var processes = new List<ApprovalProcessSettingsVm>
            {
                new ApprovalProcessSettingsVm
                {
                    ProcessCode = ApprovalProcessCodes.Transfer,
                    Stages = new List<ApprovalStageSettingsVm> { new ApprovalStageSettingsVm { RoleId = 45 }, new ApprovalStageSettingsVm { RoleId = 131 } }
                }
            };

            var warning = NewValidator().DropForeignStages(13, processes, "Asset import", "Asset", null);

            Assert.IsNotNull(warning);
            StringAssert.Contains("45", warning);
            Assert.AreEqual(1, processes[0].Stages.Count);
            Assert.AreEqual(131, processes[0].Stages[0].RoleId);
            StringAssert.Contains("outcome=dropped", _audit.Entries.Single().NewValues);
        }

        [Test]
        public void GuardStoredStages_IncidentValues_AreCleanedAndAudited()
        {
            var result = NewValidator().GuardStoredStages(13, "45,41", null, "Asset.TransferApprovalStageRoleIds", "Asset edit", "Asset", "1001");

            Assert.IsTrue(result.HasDrops);
            Assert.AreEqual(0, result.RoleIds.Count);
            Assert.AreEqual(1, _audit.Entries.Count);
            StringAssert.Contains("column=Asset.TransferApprovalStageRoleIds", _audit.Entries[0].OldValues);
        }

        [Test]
        public void GuardStoredStages_CleanValues_NoAudit()
        {
            var result = NewValidator().GuardStoredStages(13, "130,131", "u13-a", "Asset.DisposalApprovalStageRoleIds", "Asset edit", "Asset", "1001");

            Assert.IsFalse(result.HasDrops);
            Assert.AreEqual(0, _audit.Entries.Count);
        }

        [Test]
        public void Report_AuditFailure_DoesNotBreakTheSave()
        {
            var validator = new OrgReferenceValidator(_unitOfWork, new ThrowingAuditWriter(), _cache);

            Assert.DoesNotThrow(() => validator.GuardStoredStages(13, "45", null, "col", "ctx", "Asset", "1"));
        }

        internal sealed class AuditEntry
        {
            public string Action { get; set; }
            public string EntityType { get; set; }
            public string EntityId { get; set; }
            public string OldValues { get; set; }
            public string NewValues { get; set; }
        }

        internal sealed class RecordingAuditWriter : IAuditWriter
        {
            public readonly List<AuditEntry> Entries = new List<AuditEntry>();

            public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
            {
                Entries.Add(new AuditEntry { Action = action, EntityType = entityType, EntityId = entityId, OldValues = oldValues, NewValues = newValues });
            }
        }

        private sealed class ThrowingAuditWriter : IAuditWriter
        {
            public void Write(string action, string entityType, string entityId, string oldValues, string newValues)
            {
                throw new InvalidOperationException("audit store down");
            }
        }

        internal sealed class StubReferenceDataCache : IReferenceDataCache
        {
            public readonly Dictionary<int, List<RoleVm>> Roles = new Dictionary<int, List<RoleVm>>();
            public readonly Dictionary<int, List<UserVm>> Users = new Dictionary<int, List<UserVm>>();

            public IList<DepartmentVm> GetDepartments(int organizationId, bool activeOnly = true) { return new List<DepartmentVm>(); }

            public IList<RoleVm> GetRoles(int organizationId)
            {
                List<RoleVm> roles;
                return Roles.TryGetValue(organizationId, out roles) ? roles : new List<RoleVm>();
            }

            public IDictionary<string, string> GetSettings(int organizationId) { return new Dictionary<string, string>(); }

            public IList<UserVm> GetUsersForDropdown(int organizationId, int? departmentId = null) { return new List<UserVm>(); }

            public IList<UserVm> GetUsersByIds(int organizationId, IEnumerable<string> userIds)
            {
                List<UserVm> users;
                if (!Users.TryGetValue(organizationId, out users))
                {
                    return new List<UserVm>();
                }

                var wanted = new HashSet<string>(userIds ?? new string[0], StringComparer.OrdinalIgnoreCase);
                return users.Where(u => wanted.Contains(u.Id)).ToList();
            }

            public IList<CategoryLookupVm> GetCategories(int organizationId, bool activeOnly = true) { return new List<CategoryLookupVm>(); }

            public IList<AssetTypeLookupVm> GetAssetTypes(int organizationId, bool activeOnly = true) { return new List<AssetTypeLookupVm>(); }

            public IList<SupplierVm> GetSuppliers(int organizationId, bool activeOnly = true) { return new List<SupplierVm>(); }

            public void InvalidateDepartments(int organizationId) { }

            public void InvalidateRoles(int organizationId) { }

            public void InvalidateSettings(int organizationId) { }

            public void InvalidateUsers(int organizationId) { }
        }
    }
}
