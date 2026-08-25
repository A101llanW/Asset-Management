using System.Linq;

namespace AssetManagement.Application.Contracts.Security
{
    public interface IOrganizationScopeService
    {
        int? GetCurrentOrganizationId();

        int? GetTenantFilterOrganizationId(System.Type entityType);

        void SetOrganizationFilterOverride(int? organizationId);

        void SetPlatformAdminOverride(bool isPlatformAdmin);

        void SetCompanyAdminOverride(bool isCompanyAdmin);

        void SetExecutionContext(TenantExecutionContext context);

        void ClearExecutionContext();

        bool IsImpersonating();

        bool IsPlatformAdmin();

        bool IsActualPlatformAdmin();

        bool IsCompanyAdmin();

        string GetImpersonationReason();

        IQueryable<T> ApplyOrganizationFilter<T>(IQueryable<T> query) where T : class;
    }
}
