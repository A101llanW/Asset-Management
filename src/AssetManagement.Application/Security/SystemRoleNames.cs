namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Canonical system role display names. Prefer IsSystemRole + these names
    /// (or permission codes) over bare string compares for authorization.
    /// </summary>
    public static class SystemRoleNames
    {
        public const string PlatformAdmin = "Platform Admin";
        public const string CompanyAdmin = "Company Admin";
        public const string DepartmentHead = "Department Head";
        public const string AssetManager = "Asset Manager";
        public const string Staff = "Staff";

        public static bool IsReservedName(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            return string.Equals(roleName.Trim(), PlatformAdmin, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleName.Trim(), CompanyAdmin, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleName.Trim(), DepartmentHead, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleName.Trim(), AssetManager, System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(roleName.Trim(), Staff, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsCompanyAdmin(string roleName, bool isSystemRole)
        {
            return isSystemRole
                && string.Equals(roleName, CompanyAdmin, System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPlatformAdmin(string roleName, bool isSystemRole)
        {
            return isSystemRole
                && string.Equals(roleName, PlatformAdmin, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
