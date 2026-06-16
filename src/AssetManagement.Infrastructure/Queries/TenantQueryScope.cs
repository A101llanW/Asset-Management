using System;
using System.Data;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;

namespace AssetManagement.Infrastructure.Queries
{
    public sealed class TenantQueryScope
    {
        public int OrganizationId { get; private set; }

        public int? DepartmentId { get; private set; }

        public bool BypassesDepartmentScope { get; private set; }

        public bool DenyDepartmentScope { get; private set; }

        public static TenantQueryScope Resolve(IOrganizationScopeService organizationScope, IDepartmentScopeService departmentScope)
        {
            if (organizationScope == null)
            {
                throw new InvalidOperationException("Organization scope is required for SQL queries.");
            }

            var organizationId = organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                throw new InvalidOperationException("Organization context is required for SQL queries.");
            }

            var bypassesDepartmentScope = departmentScope != null && departmentScope.BypassesDepartmentScope;
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (departmentScope != null && !bypassesDepartmentScope)
            {
                departmentId = departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            return new TenantQueryScope
            {
                OrganizationId = organizationId.Value,
                DepartmentId = departmentId,
                BypassesDepartmentScope = bypassesDepartmentScope,
                DenyDepartmentScope = denyDepartmentScope
            };
        }

        public void AddScopeParameters(IDbCommand command)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", OrganizationId);
            SqlQueryHelper.AddDepartmentScopeParameters(command, BypassesDepartmentScope, DenyDepartmentScope, DepartmentId);
        }
    }
}
