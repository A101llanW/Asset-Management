using System;

namespace AssetManagement.Application.Helpers
{
    public static class DemoLoginEmailHelper
    {
        public const string PlatformAdminEmail = "superadmin@asset.local";
        public const string LegacyDefaultTenantAdminEmail = "default@asset.local";
        public const string PrimaryDemoTenantSlug = "nanosoft";

        public static string ResolveLoginEmail(string email, string organizationSlug)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return email;
            }

            var normalized = email.Trim();
            if (!normalized.Contains("@"))
            {
                normalized = normalized + "@asset.local";
            }

            if (IsLegacyDefaultTenantAdminEmail(normalized))
            {
                return BuildCompanyAdminEmail(PrimaryDemoTenantSlug);
            }

            if (!string.IsNullOrWhiteSpace(organizationSlug))
            {
                return ResolveTenantLoginEmail(normalized, organizationSlug);
            }

            return normalized;
        }

        public static string ResolveTenantLoginEmail(string email, string organizationSlug)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(organizationSlug))
            {
                return email == null ? null : email.Trim();
            }

            var normalized = email.Trim();
            if (!normalized.Contains("@"))
            {
                normalized = normalized + "@asset.local";
            }

            if (IsLegacyTenantAdminEmail(normalized, organizationSlug))
            {
                return BuildCompanyAdminEmail(organizationSlug);
            }

            return normalized;
        }

        public static string BuildOrganizationContactEmail(string organizationSlug)
        {
            if (string.IsNullOrWhiteSpace(organizationSlug))
            {
                return null;
            }

            return "admin@" + organizationSlug.Trim().ToLowerInvariant() + ".asset.local";
        }

        public static string BuildCompanyAdminEmail(string organizationSlug)
        {
            if (string.IsNullOrWhiteSpace(organizationSlug))
            {
                return null;
            }

            if (string.Equals(organizationSlug.Trim(), "demo-b", StringComparison.OrdinalIgnoreCase))
            {
                return "demo@asset.local";
            }

            return organizationSlug.Trim().ToLowerInvariant() + "@asset.local";
        }

        public static bool IsPlatformAdminEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email)
                && email.Trim().Equals(PlatformAdminEmail, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsLegacyDefaultTenantAdminEmail(string email)
        {
            return !string.IsNullOrWhiteSpace(email)
                && email.Trim().Equals(LegacyDefaultTenantAdminEmail, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsLegacyTenantAdminEmail(string email, string organizationSlug)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            if (IsLegacyDefaultTenantAdminEmail(email)
                || string.Equals(email, PlatformAdminEmail, StringComparison.OrdinalIgnoreCase)
                || string.Equals(email, "companyadmin@asset.local", StringComparison.OrdinalIgnoreCase)
                || string.Equals(email, "platform@asset.local", StringComparison.OrdinalIgnoreCase)
                || email.EndsWith("@demo-b.asset.local", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(organizationSlug))
            {
                return false;
            }

            var contactEmail = BuildOrganizationContactEmail(organizationSlug);
            return string.Equals(email, contactEmail, StringComparison.OrdinalIgnoreCase);
        }
    }
}
