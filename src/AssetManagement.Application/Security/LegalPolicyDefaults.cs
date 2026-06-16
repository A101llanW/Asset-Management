using System;

namespace AssetManagement.Application.Security
{
    public enum LegalRelationshipKind
    {
        TenantUser,
        CompanyAdmin,
        PlatformAdmin
    }

    public static class LegalPolicyDefaults
    {
        public const string CompanyName = "Nanosoft";
        public const string ContactEmail = "nanosoft.africa@gmail.com";
        public const string ContactAddress = "Nairobi, Kenya";
        public const string LastUpdated = "June 14, 2026";

        public const string TenantUserPrivacyVersion = "2026-06-14-T";
        public const string TenantUserTermsVersion = "2026-06-14-T";
        public const string CompanyAdminPrivacyVersion = "2026-06-14-C";
        public const string CompanyAdminTermsVersion = "2026-06-14-C";
        public const string PlatformAdminPrivacyVersion = "2026-06-14-P";
        public const string PlatformAdminTermsVersion = "2026-06-14-P";

        public static string GetPrivacyVersion(LegalRelationshipKind relationship)
        {
            switch (relationship)
            {
                case LegalRelationshipKind.PlatformAdmin:
                    return PlatformAdminPrivacyVersion;
                case LegalRelationshipKind.CompanyAdmin:
                    return CompanyAdminPrivacyVersion;
                default:
                    return TenantUserPrivacyVersion;
            }
        }

        public static string GetTermsVersion(LegalRelationshipKind relationship)
        {
            switch (relationship)
            {
                case LegalRelationshipKind.PlatformAdmin:
                    return PlatformAdminTermsVersion;
                case LegalRelationshipKind.CompanyAdmin:
                    return CompanyAdminTermsVersion;
                default:
                    return TenantUserTermsVersion;
            }
        }

        public static string GetRelationshipDisplayName(LegalRelationshipKind relationship)
        {
            switch (relationship)
            {
                case LegalRelationshipKind.PlatformAdmin:
                    return "platform operator";
                case LegalRelationshipKind.CompanyAdmin:
                    return "company administrator";
                default:
                    return "tenant user";
            }
        }

        public static LegalRelationshipKind ResolveFromRoleAndOrganization(string roleName, int? organizationId)
        {
            if (!organizationId.HasValue)
            {
                return LegalRelationshipKind.PlatformAdmin;
            }

            if (string.Equals(roleName, "Company Admin", StringComparison.OrdinalIgnoreCase))
            {
                return LegalRelationshipKind.CompanyAdmin;
            }

            return LegalRelationshipKind.TenantUser;
        }
    }
}
