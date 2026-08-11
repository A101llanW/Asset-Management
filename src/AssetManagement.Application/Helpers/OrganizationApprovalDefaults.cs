using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Canonical approval-stage role names per process. Resolved to tenant role ids at provisioning time.
    /// </summary>
    public static class OrganizationApprovalDefaults
    {
        public const string CompanyAdminRoleName = "Company Admin";
        public const string DepartmentHeadRoleName = "Department Head";
        public const string AssetManagerRoleName = "Asset Manager";
        public const string ProcurementOfficerRoleName = "Procurement Officer";
        public const string ProcurementManagerRoleName = "Procurement Manager";
        public const string FacilitiesManagerRoleName = "Facilities Manager";

        private static readonly string[] ApprovalSettingKeyPrefixes =
        {
            "Approval.Require",
            "Approval.Process."
        };

        public static string GetDefaultApproverRoleName(string processCode)
        {
            switch (processCode)
            {
                case ApprovalProcessCodes.Transfer:
                    return DepartmentHeadRoleName;
                case ApprovalProcessCodes.Disposal:
                    return CompanyAdminRoleName;
                case ApprovalProcessCodes.Purchase:
                    return ProcurementManagerRoleName;
                default:
                    return DepartmentHeadRoleName;
            }
        }

        public static int? ResolveRoleId(IEnumerable<Role> roles, string roleName)
        {
            if (roles == null || string.IsNullOrWhiteSpace(roleName))
            {
                return null;
            }

            var match = roles.FirstOrDefault(r =>
                r != null
                && r.IsActive
                && string.Equals(r.Name, roleName.Trim(), StringComparison.OrdinalIgnoreCase));

            return match == null || match.Id <= 0 ? (int?)null : match.Id;
        }

        public static int? ResolveDefaultApproverRoleId(string processCode, IEnumerable<Role> roles)
        {
            var primary = ResolveRoleId(roles, GetDefaultApproverRoleName(processCode));
            if (primary.HasValue)
            {
                return primary;
            }

            if (string.Equals(processCode, ApprovalProcessCodes.Purchase, StringComparison.OrdinalIgnoreCase))
            {
                return ResolveRoleId(roles, ProcurementOfficerRoleName);
            }

            return null;
        }

        public static int? ResolveDefaultApproverRoleId(string processCode, IEnumerable<RoleVm> roles)
        {
            if (roles == null)
            {
                return null;
            }

            var roleName = GetDefaultApproverRoleName(processCode);
            var match = roles.FirstOrDefault(r =>
                r != null
                && r.Id > 0
                && string.Equals(r.Name, roleName, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match.Id;
            }

            if (string.Equals(processCode, ApprovalProcessCodes.Purchase, StringComparison.OrdinalIgnoreCase))
            {
                var fallback = roles.FirstOrDefault(r =>
                    r != null
                    && r.Id > 0
                    && string.Equals(r.Name, ProcurementOfficerRoleName, StringComparison.OrdinalIgnoreCase));
                return fallback == null ? (int?)null : fallback.Id;
            }

            return null;
        }

        public static bool IsApprovalSettingKey(string settingKey)
        {
            if (string.IsNullOrWhiteSpace(settingKey))
            {
                return false;
            }

            foreach (var prefix in ApprovalSettingKeyPrefixes)
            {
                if (settingKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool StageRoleIdsAreValidForOrganization(string stageRoleIds, ICollection<int> organizationRoleIds)
        {
            var parsed = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(stageRoleIds);
            if (parsed.Count == 0)
            {
                return false;
            }

            if (organizationRoleIds == null || organizationRoleIds.Count == 0)
            {
                return false;
            }

            return parsed.All(organizationRoleIds.Contains);
        }

        public static void EnsureApprovalSettings(
            IEnumerable<SystemSetting> existingSettings,
            ICollection<Role> organizationRoles,
            int organizationId,
            DateTime now,
            Action<SystemSetting> addSetting,
            Action<SystemSetting> updateSetting,
            bool refreshInvalidStageRoleIds)
        {
            if (organizationRoles == null || organizationRoles.Count == 0 || addSetting == null)
            {
                return;
            }

            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(existingSettings);
            var roleIds = new HashSet<int>(organizationRoles.Where(r => r != null && r.IsActive && r.Id > 0).Select(r => r.Id));

            foreach (var processCode in ApprovalProcessCodes.Ordered)
            {
                EnsureProcessApprovalSettings(
                    processCode,
                    settings,
                    organizationRoles,
                    roleIds,
                    organizationId,
                    now,
                    addSetting,
                    updateSetting,
                    refreshInvalidStageRoleIds);
            }
        }

        private static void EnsureProcessApprovalSettings(
            string processCode,
            IDictionary<string, SystemSetting> settings,
            ICollection<Role> organizationRoles,
            ICollection<int> organizationRoleIds,
            int organizationId,
            DateTime now,
            Action<SystemSetting> addSetting,
            Action<SystemSetting> updateSetting,
            bool refreshInvalidStageRoleIds)
        {
            var approverRoleId = ResolveDefaultApproverRoleId(processCode, organizationRoles);
            if (!approverRoleId.HasValue)
            {
                return;
            }

            var stageRoleIdsValue = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(new[] { approverRoleId });
            var stageRoleKey = ApprovalProcessCodes.GetStageRoleIdsSettingKey(processCode);
            var stageUserKey = ApprovalProcessCodes.GetStageUserIdsSettingKey(processCode);
            var enabledKey = ApprovalProcessCodes.GetEnabledSettingKey(processCode);
            var legacyKey = ApprovalProcessCodes.GetLegacyRequireSettingKey(processCode);

            SystemSetting stageSetting;
            var hasStageSetting = settings.TryGetValue(stageRoleKey, out stageSetting);
            var shouldWriteStageRoleIds = !hasStageSetting
                || refreshInvalidStageRoleIds
                || !StageRoleIdsAreValidForOrganization(stageSetting?.SettingValue, organizationRoleIds);

            if (shouldWriteStageRoleIds)
            {
                UpsertSetting(
                    settings,
                    stageRoleKey,
                    stageRoleIdsValue,
                    "Stage 1 approver role ids for " + ApprovalProcessCodes.GetDisplayName(processCode) + ".",
                    organizationId,
                    now,
                    addSetting,
                    updateSetting);
            }

            UpsertSettingIfMissing(
                settings,
                stageUserKey,
                string.Empty,
                "Ordered approver user ids for " + ApprovalProcessCodes.GetDisplayName(processCode) + " approval stages.",
                organizationId,
                now,
                addSetting);

            UpsertSettingIfMissing(
                settings,
                enabledKey,
                "false",
                "Whether " + ApprovalProcessCodes.GetDisplayName(processCode) + " requires staged approval.",
                organizationId,
                now,
                addSetting);

            if (!string.IsNullOrWhiteSpace(legacyKey))
            {
                UpsertSettingIfMissing(
                    settings,
                    legacyKey,
                    "false",
                    "Legacy approval toggle for " + ApprovalProcessCodes.GetDisplayName(processCode) + ".",
                    organizationId,
                    now,
                    addSetting);
            }
        }

        private static void UpsertSettingIfMissing(
            IDictionary<string, SystemSetting> settings,
            string key,
            string value,
            string description,
            int organizationId,
            DateTime now,
            Action<SystemSetting> addSetting)
        {
            SystemSetting existing;
            if (settings.ContainsKey(key))
            {
                return;
            }

            var setting = new SystemSetting
            {
                SettingKey = key,
                SettingValue = value,
                Description = description,
                OrganizationId = organizationId,
                CreatedAt = now,
                IsActive = true
            };
            addSetting(setting);
            settings[key] = setting;
        }

        private static void UpsertSetting(
            IDictionary<string, SystemSetting> settings,
            string key,
            string value,
            string description,
            int organizationId,
            DateTime now,
            Action<SystemSetting> addSetting,
            Action<SystemSetting> updateSetting)
        {
            SystemSetting existing;
            if (settings.TryGetValue(key, out existing) && existing != null)
            {
                existing.SettingValue = value;
                existing.Description = description;
                if (updateSetting != null)
                {
                    updateSetting(existing);
                }

                return;
            }

            var setting = new SystemSetting
            {
                SettingKey = key,
                SettingValue = value,
                Description = description,
                OrganizationId = organizationId,
                CreatedAt = now,
                IsActive = true
            };
            addSetting(setting);
            settings[key] = setting;
        }
    }
}
