using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    /// <summary>
    /// Server-side same-organisation guard shared by every write path that stores ids of org-scoped rows.
    /// Interactive forms call the Ensure* methods (foreign/missing ids are rejected with a BusinessException the
    /// controllers already show in the form summary); bulk/import/copy paths call the Drop* methods (foreign ids are
    /// removed and a warning returned). Every foreign id found is written to the audit log
    /// (<see cref="OrgReferenceGuard.AuditAction"/>) and to the app trace log. Lookups are memoised per instance:
    /// construct one per request/operation.
    /// </summary>
    public sealed class OrgReferenceValidator
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IReferenceDataCache _referenceDataCache;
        private readonly IUserService _userService;
        private readonly Dictionary<int, HashSet<int>> _activeRoleIds = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<int, HashSet<int>> _allRoleIds = new Dictionary<int, HashSet<int>>();
        private readonly Dictionary<string, bool> _userCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public OrgReferenceValidator(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IReferenceDataCache referenceDataCache = null,
            IUserService userService = null)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _referenceDataCache = referenceDataCache;
            _userService = userService;
        }

        // ---------------------------------------------------------------- membership tests

        public bool RoleInOrganization(int organizationId, int roleId)
        {
            if (roleId <= 0)
            {
                return false;
            }

            HashSet<int> active;
            if (!_activeRoleIds.TryGetValue(organizationId, out active))
            {
                active = new HashSet<int>();
                if (_referenceDataCache != null)
                {
                    foreach (var role in _referenceDataCache.GetRoles(organizationId) ?? new List<RoleVm>())
                    {
                        if (role != null && role.Id > 0)
                        {
                            active.Add(role.Id);
                        }
                    }
                }

                _activeRoleIds[organizationId] = active;
            }

            if (active.Contains(roleId))
            {
                return true;
            }

            // Inactive roles of the same organisation are not cross-org: load the full org list once.
            HashSet<int> all;
            if (!_allRoleIds.TryGetValue(organizationId, out all))
            {
                all = new HashSet<int>();
                if (_unitOfWork != null)
                {
                    var orgId = (int?)organizationId;
                    foreach (var role in _unitOfWork.Repository<Role>().Find(r => r.OrganizationId == orgId))
                    {
                        if (role != null && role.Id > 0 && role.OrganizationId == organizationId)
                        {
                            all.Add(role.Id);
                        }
                    }
                }

                _allRoleIds[organizationId] = all;
            }

            return all.Contains(roleId);
        }

        public bool UserInOrganization(int organizationId, string userId)
        {
            var id = string.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
            if (id == null)
            {
                return false;
            }

            var key = organizationId + "|" + id;
            bool cached;
            if (_userCache.TryGetValue(key, out cached))
            {
                return cached;
            }

            bool found;
            if (_referenceDataCache != null)
            {
                // Explicit org filter in SQL (Users.OrganizationId = @OrganizationId).
                found = (_referenceDataCache.GetUsersByIds(organizationId, new[] { id }) ?? new List<UserVm>())
                    .Any(x => x != null && string.Equals((x.Id ?? string.Empty).Trim(), id, StringComparison.OrdinalIgnoreCase));
            }
            else if (_userService != null)
            {
                // Tenant-filtered by the current organisation (UserService.GetById returns null for other orgs).
                found = _userService.GetById(id) != null;
            }
            else
            {
                // No way to check: do not drop data we cannot verify.
                found = true;
            }

            _userCache[key] = found;
            return found;
        }

        public bool DepartmentInOrganization(int organizationId, int departmentId)
        {
            var orgId = (int?)organizationId;
            return departmentId > 0 && _unitOfWork.Repository<Department>().Count(x => x.Id == departmentId && x.OrganizationId == orgId) > 0;
        }

        public bool CategoryInOrganization(int organizationId, int categoryId)
        {
            var orgId = (int?)organizationId;
            return categoryId > 0 && _unitOfWork.Repository<AssetCategory>().Count(x => x.Id == categoryId && x.OrganizationId == orgId) > 0;
        }

        public bool AssetTypeInOrganization(int organizationId, int assetTypeId)
        {
            var orgId = (int?)organizationId;
            return assetTypeId > 0 && _unitOfWork.Repository<AssetType>().Count(x => x.Id == assetTypeId && x.OrganizationId == orgId) > 0;
        }

        public bool AssetSubTypeInOrganization(int organizationId, int assetSubTypeId)
        {
            var orgId = (int?)organizationId;
            return assetSubTypeId > 0 && _unitOfWork.Repository<AssetSubType>().Count(x => x.Id == assetSubTypeId && x.OrganizationId == orgId) > 0;
        }

        public bool SupplierInOrganization(int organizationId, int supplierId)
        {
            var orgId = (int?)organizationId;
            return supplierId > 0 && _unitOfWork.Repository<Supplier>().Count(x => x.Id == supplierId && x.OrganizationId == orgId) > 0;
        }

        // ---------------------------------------------------------------- interactive (reject)

        /// <summary>Rejects posted approval stages whose role/person is not in the organisation.</summary>
        public void EnsureProcessesInOrganization(
            int organizationId,
            IList<ApprovalProcessSettingsVm> processes,
            string context,
            string entityType,
            string entityId)
        {
            var problems = OrgReferenceGuard.FindForeignStages(
                processes,
                id => RoleInOrganization(organizationId, id),
                id => UserInOrganization(organizationId, id));
            if (problems.Count == 0)
            {
                return;
            }

            Report(organizationId, context, entityType, entityId, "ApprovalStages", string.Join(" | ", problems.ToArray()), true);
            throw new BusinessException(string.Join(" ", problems.ToArray()));
        }

        /// <summary>
        /// Rejects an approval-path snapshot (transfer/disposal/requisition submit) that would copy a foreign role
        /// or person onto the new request. Better to stop the submission than to create a request nobody in this
        /// company can approve (or one that silently loses a stage).
        /// </summary>
        public void EnsureConfigurationInOrganization(
            int? organizationId,
            ApprovalProcessConfiguration config,
            string context,
            string entityType,
            string entityId,
            bool checkUsers = true)
        {
            if (!organizationId.HasValue || config == null)
            {
                return;
            }

            var org = organizationId.Value;
            var check = OrgReferenceGuard.GuardStageCsv(
                ApprovalWorkflowSettingsHelper.SerializeStageRoleIds((config.StageRoleIds ?? new List<int>()).Select(x => (int?)x)),
                ApprovalWorkflowSettingsHelper.SerializeStageUserIds(config.StageUserIds),
                id => RoleInOrganization(org, id),
                checkUsers ? (Func<string, bool>)(id => UserInOrganization(org, id)) : null);
            if (!check.HasDrops)
            {
                return;
            }

            Report(org, context, entityType, entityId, "ApprovalStageRoleIds/ApprovalStageUserIds", check.DescribeDrops(), true);
            throw new BusinessException(
                "The " + (string.IsNullOrWhiteSpace(config.DisplayName) ? "approval" : config.DisplayName)
                + " approval path references a role or person from another company (" + check.DescribeDrops()
                + "). Ask a Company Admin to fix the approval stages (Settings > Approval Matrix, the department's requisition flow, or the asset's approval settings), then submit again.");
        }

        /// <summary>Rejects a foreign/missing department, category, type, sub-type or supplier id.</summary>
        public void EnsureAssetReferencesInOrganization(
            int organizationId,
            int? departmentId,
            int? categoryId,
            int? assetTypeId,
            int? assetSubTypeId,
            int? supplierId,
            string context,
            string entityId)
        {
            var problems = new List<string>();
            if (!OrgReferenceGuard.IsEmptyOrAllowed(departmentId, id => DepartmentInOrganization(organizationId, id)))
            {
                problems.Add("department #" + departmentId);
            }

            if (!OrgReferenceGuard.IsEmptyOrAllowed(categoryId, id => CategoryInOrganization(organizationId, id)))
            {
                problems.Add("category #" + categoryId);
            }

            if (!OrgReferenceGuard.IsEmptyOrAllowed(assetTypeId, id => AssetTypeInOrganization(organizationId, id)))
            {
                problems.Add("asset type #" + assetTypeId);
            }

            if (!OrgReferenceGuard.IsEmptyOrAllowed(assetSubTypeId, id => AssetSubTypeInOrganization(organizationId, id)))
            {
                problems.Add("sub-type #" + assetSubTypeId);
            }

            if (!OrgReferenceGuard.IsEmptyOrAllowed(supplierId, id => SupplierInOrganization(organizationId, id)))
            {
                problems.Add("supplier #" + supplierId);
            }

            if (problems.Count == 0)
            {
                return;
            }

            var detail = string.Join(", ", problems.ToArray());
            Report(organizationId, context, "Asset", entityId, "Asset FKs", detail, true);
            throw new BusinessException("The selected " + detail + " does not belong to this company. Choose values from this company's lists.");
        }

        /// <summary>Rejects a role id (e.g. PO raiser) that is not in the organisation.</summary>
        public void EnsureRoleInOrganization(int organizationId, int? roleId, string label, string context, string entityType, string entityId)
        {
            if (!roleId.HasValue || roleId.Value <= 0 || RoleInOrganization(organizationId, roleId.Value))
            {
                return;
            }

            Report(organizationId, context, entityType, entityId, label, roleId.Value.ToString(), true);
            throw new BusinessException(label + ": " + OrgReferenceGuard.ForeignRoleMessage + " (role #" + roleId.Value + ")");
        }

        /// <summary>Rejects a user id (class teacher, room custodian, PO raiser person) not in the organisation.</summary>
        public void EnsureUserInOrganization(int organizationId, string userId, string label, string context, string entityType, string entityId)
        {
            if (string.IsNullOrWhiteSpace(userId) || UserInOrganization(organizationId, userId))
            {
                return;
            }

            Report(organizationId, context, entityType, entityId, label, userId.Trim(), true);
            throw new BusinessException(label + ": the selected person is not a user in this company.");
        }

        /// <summary>Rejects a department id (parent, target) not in the organisation.</summary>
        public void EnsureDepartmentInOrganization(int organizationId, int? departmentId, string label, string context, string entityType, string entityId)
        {
            if (!departmentId.HasValue || departmentId.Value <= 0 || DepartmentInOrganization(organizationId, departmentId.Value))
            {
                return;
            }

            Report(organizationId, context, entityType, entityId, label, departmentId.Value.ToString(), true);
            throw new BusinessException(label + ": the selected unit does not belong to this company.");
        }

        // ---------------------------------------------------------------- bulk / import / stored values (drop)

        /// <summary>Drop mode for posted/default stages (import, bulk). Returns a warning, or null when clean.</summary>
        public string DropForeignStages(
            int organizationId,
            IList<ApprovalProcessSettingsVm> processes,
            string context,
            string entityType,
            string entityId)
        {
            var dropped = OrgReferenceGuard.DropForeignStages(
                processes,
                id => RoleInOrganization(organizationId, id),
                id => UserInOrganization(organizationId, id));
            if (dropped.Count == 0)
            {
                return null;
            }

            var detail = string.Join(" | ", dropped.Select(x => x.Key + ": " + x.Value.DescribeDrops()).ToArray());
            Report(organizationId, context, entityType, entityId, "ApprovalStages", detail, false);
            return "Approval stages from another company were removed (" + detail + ").";
        }

        /// <summary>
        /// Checks a stored CSV pair and returns the cleaned values. Drops are logged; callers write
        /// <see cref="StageGuardResult.RoleCsv"/>/<see cref="StageGuardResult.UserCsv"/> back only when
        /// <see cref="StageGuardResult.HasDrops"/> (so clean rows keep their exact stored text).
        /// </summary>
        public StageGuardResult GuardStoredStages(
            int organizationId,
            string roleCsv,
            string userCsv,
            string column,
            string context,
            string entityType,
            string entityId)
        {
            var result = OrgReferenceGuard.GuardStageCsv(
                roleCsv,
                userCsv,
                id => RoleInOrganization(organizationId, id),
                id => UserInOrganization(organizationId, id));
            if (result.HasDrops)
            {
                Report(organizationId, context, entityType, entityId, column, result.DescribeDrops(), false);
            }

            return result;
        }

        // ---------------------------------------------------------------- logging

        public void Report(int organizationId, string context, string entityType, string entityId, string column, string ids, bool rejected)
        {
            var outcome = rejected ? "rejected" : "dropped";
            var detail = OrgReferenceGuard.BuildAuditDetail(organizationId, column, ids);
            try
            {
                Trace.TraceWarning(
                    "[CrossOrgReference] {0} {1} {2}#{3} {4}",
                    outcome,
                    context ?? string.Empty,
                    entityType ?? string.Empty,
                    entityId ?? string.Empty,
                    detail);
            }
            catch
            {
                // Logging must never break the save.
            }

            if (_auditWriter == null)
            {
                return;
            }

            try
            {
                _auditWriter.Write(
                    OrgReferenceGuard.AuditAction,
                    entityType ?? "Unknown",
                    entityId,
                    detail,
                    "outcome=" + outcome + ";context=" + (context ?? string.Empty));
            }
            catch (Exception ex)
            {
                try
                {
                    Trace.TraceError("[CrossOrgReference] audit write failed: " + ex.Message);
                }
                catch
                {
                }
            }
        }
    }
}
