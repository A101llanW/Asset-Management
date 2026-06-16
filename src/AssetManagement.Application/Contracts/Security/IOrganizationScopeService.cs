using System.Linq;

namespace AssetManagement.Application.Contracts.Security
{
    public interface IOrganizationScopeService
    {
        int? GetCurrentOrganizationId();

        int? GetTenantFilterOrganizationId(System.Type entityType);

        void SetOrganizationFilterOverride(int? organizationId);

        bool IsImpersonating();

        bool IsPlatformAdmin();

        bool IsActualPlatformAdmin();

        bool IsCompanyAdmin();

        string GetImpersonationReason();

        IQueryable<T> ApplyOrganizationFilter<T>(IQueryable<T> query) where T : class;
    }
}
