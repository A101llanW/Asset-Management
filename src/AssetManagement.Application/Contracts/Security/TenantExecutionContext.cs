namespace AssetManagement.Application.Contracts.Security
{
    public sealed class TenantExecutionContext
    {
        public int? OrganizationId { get; set; }

        public int? OrganizationFilterOverride { get; set; }

        public bool IsImpersonating { get; set; }

        public bool IsPlatformAdmin { get; set; }

        public bool IsCompanyAdmin { get; set; }

        public string ImpersonationReason { get; set; }
    }
}
