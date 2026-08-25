using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Web.Helpers
{
    public static class TenantResolutionHelper
    {
        public static int? ResolveOrganizationId(IUnitOfWork unitOfWork, ISqlConnectionFactory connectionFactory, string tenantToken)
        {
            if (string.IsNullOrWhiteSpace(tenantToken))
            {
                return null;
            }

            if (connectionFactory != null)
            {
                var repository = new UserAccountRepository(connectionFactory);
                var organizationId = repository.FindOrganizationIdByTenantToken(tenantToken);
                if (organizationId.HasValue)
                {
                    return organizationId;
                }
            }

            return ResolveOrganization(unitOfWork, tenantToken)?.Id;
        }

        public static Organization ResolveOrganization(IUnitOfWork unitOfWork, string tenantToken)
        {
            if (unitOfWork == null || string.IsNullOrWhiteSpace(tenantToken))
            {
                return null;
            }

            var normalized = tenantToken.Trim();
            return unitOfWork.Repository<Organization>().Query()
                .FirstOrDefault(o => o.IsActive
                    && ((o.Slug != null && o.Slug.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))
                        || (o.AccessToken != null && o.AccessToken.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))
                        || (o.Code != null && o.Code.Equals(normalized, System.StringComparison.OrdinalIgnoreCase))));
        }
    }
}
