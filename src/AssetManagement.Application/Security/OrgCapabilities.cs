using System;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Single place for "who is the tenant administrator" and "who has organization-wide scope".
    /// Callers should never compare role names themselves.
    ///   - IsTenantAdministratorRole: identity of the system Company Admin role (escalation guards,
    ///     last-admin rule, platform listings). Name + IsSystemRole, so a renamed custom role cannot match.
    ///   - HasOrgWideScope: capability (permission Assets.ViewAll). Bypasses department scoping for assets,
    ///     departments, lists and metrics. Company Admin keeps it through the AuthorizationService bypass
    ///     and the 099 seed; custom roles get it by ticking "Assets.ViewAll" in the role editor.
    /// </summary>
    public static class OrgCapabilities
    {
        public const string ViewAllAssetsPermission = "Assets.ViewAll";

        public const string TenantAdministratorRoleName = SystemRoleNames.CompanyAdmin;

        /// <summary>SQL predicate (alias r = Roles) matching the system Company Admin role.</summary>
        public const string TenantAdministratorRoleSqlPredicate =
            "r.[IsSystemRole] = 1 AND r.[Name] = N'" + SystemRoleNames.CompanyAdmin + "'";

        public static bool IsTenantAdministratorRole(string roleName, bool isSystemRole)
        {
            return SystemRoleNames.IsCompanyAdmin(roleName, isSystemRole);
        }

        /// <summary>Name-only match, for display rows that carry no IsSystemRole flag (platform listings).</summary>
        public static bool IsTenantAdministratorRoleName(string roleName)
        {
            return string.Equals(roleName, SystemRoleNames.CompanyAdmin, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasOrgWideScope(IAuthorizationService authorizationService, string userId)
        {
            return authorizationService != null
                && !string.IsNullOrWhiteSpace(userId)
                && authorizationService.HasPermission(userId, ViewAllAssetsPermission);
        }
    }
}
