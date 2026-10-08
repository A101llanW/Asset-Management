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

        /// <summary>
        /// Transfers + assignments dated within [fromDate, toDate] (whole days) on department-scoped
        /// assets of the organization. Same rule as MetricsService.CountCustodyMovements, counted in SQL.
        /// </summary>
        int CountCustodyMovements(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool includeClassDepartmentAssets,
            DateTime fromDate,
            DateTime toDate);
    }
}
