using System;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Pure guardrails for organization hard-purge (platform delete). No SQL.
    /// </summary>
    public static class OrganizationPurgeConfirmation
    {
        public static bool IsValidOrganizationId(int organizationId)
        {
            return organizationId > 0;
        }

        /// <summary>
        /// Operator must type the organization name (case-insensitive, trimmed) to confirm permanent deletion.
        /// </summary>
        public static bool MatchesTypedName(string organizationName, string confirmName)
        {
            if (string.IsNullOrWhiteSpace(organizationName) || string.IsNullOrWhiteSpace(confirmName))
            {
                return false;
            }

            return string.Equals(confirmName.Trim(), organizationName.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Refuse deleting the organization currently being impersonated.
        /// </summary>
        public static bool IsImpersonatingTarget(int organizationId, int? impersonatedOrganizationId)
        {
            return impersonatedOrganizationId.HasValue && impersonatedOrganizationId.Value == organizationId;
        }
    }
}
