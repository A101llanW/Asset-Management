using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Tag/name/department of the given asset ids in the organization (active or not, like
        /// Repository&lt;Asset&gt;.GetAll), with InScope = the ApplyAssetScope department rule.
        /// Replaces loading every org asset to look up a handful of rows.
        /// </summary>
        IList<ReportAssetRefDto> GetAssetRefs(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool includeClassDepartmentAssets,
            IEnumerable<int> assetIds);

        /// <summary>
        /// Display labels ("First Last (email)", else email, else username) for user ids, usernames
        /// or emails of users in the organization, in one batched lookup. Unknown keys are absent.
        /// </summary>
        IDictionary<string, string> GetUserLabels(int organizationId, IEnumerable<string> keys);
    }

    public class ReportAssetRefDto
    {
        public int Id { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public int? DepartmentId { get; set; }

        public bool InScope { get; set; }
    }
}
