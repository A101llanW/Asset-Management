using System;
using AssetManagement.Application.DTOs;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IDashboardQueryService
    {
        DashboardKpisDto GetKpis(int organizationId, int? departmentId, bool bypassDepartmentScope, bool denyDepartmentScope);

        DashboardExpiryCountsDto GetExpiryCounts(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool includeClassDepartmentAssets,
            DateTime warrantyThresholdUtc,
            DateTime insuranceThresholdUtc);
    }
}
