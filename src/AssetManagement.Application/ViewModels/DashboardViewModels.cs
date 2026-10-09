using System;
using System.Collections.Generic;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Application.ViewModels
{
    public class DashboardVm
    {
        public int TotalAssets { get; set; }

        public int AssignedAssets { get; set; }

        public int UnassignedAssets { get; set; }

        public int MaintenanceAndDamagedAssets { get; set; }

        public int LostAssets { get; set; }

        public int LostDamagedStolenAssets { get; set; }

        public decimal TotalAcquisitionValue { get; set; }

        public decimal TotalCurrentBookValue { get; set; }

        public decimal TotalAccumulatedDepreciation { get; set; }

        public decimal AverageAnnualDepreciationRatePercent { get; set; }

        public decimal DepreciationToDatePercent { get; set; }

        public int ExpiringWarrantyCount { get; set; }

        public int ExpiringInsuranceCount { get; set; }

        public IList<DashboardTrendPointVm> AssignmentsPerMonth { get; set; }

        public int ApprovalBacklogCount { get; set; }

        public IList<DepartmentValueVm> TopDepartmentValues { get; set; }

        public decimal LossDamageRatePercent { get; set; }

        public decimal TotalCostOfOwnership { get; set; }

        public int WarrantyThresholdDays { get; set; }

        public int InsuranceThresholdDays { get; set; }

        public bool IsDepartmentScoped { get; set; }
    }

    public class DashboardTrendPointVm
    {
        public string Label { get; set; }

        public int Count { get; set; }
    }

    public class DepartmentValueVm
    {
        public string DepartmentName { get; set; }

        public decimal BookValue { get; set; }

        public int AssetCount { get; set; }
    }

    public class AuditLogFilterVm
    {
        public string EntityType { get; set; }

        /// <summary>
        /// When set, returns audit rows for the asset and related custody/incident/maintenance entities.
        /// </summary>
        public int? RelatedAssetId { get; set; }

        /// <summary>
        /// When true, excludes HTTP controller hits and other non-lifecycle audit noise.
        /// Intended for asset detail audit tabs; admin audit log views should leave this false.
        /// </summary>
        public bool BusinessEventsOnly { get; set; }

        public string Action { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        /// <summary>
        /// Optional cap for newest-first org lists (e.g. platform org details "recent audit").
        /// Ignored for related-asset history. Null returns every matching row.
        /// </summary>
        public int? MaxRows { get; set; }

        /// <summary>
        /// Action code typed in the Audit Logs filter box. Bound from the query string as ActionCode because
        /// "Action" collides with the MVC route value {action} (which bound "Index"/"Export" into Action and
        /// filtered every list to nothing). The controller copies it into <see cref="Action"/> (rel1009g).
        /// </summary>
        public string ActionCode { get; set; }

        /// <summary>Free-text actor search (user id, e-mail, or name).</summary>
        public string Actor { get; set; }

        /// <summary>When false (default) HTTP GET page-view rows (HTTP.* with VIEW:) are hidden.</summary>
        public bool IncludePageViews { get; set; }

        /// <summary>When true, sign-in successes/failures (LoginAttempts) are not merged into the list.</summary>
        public bool HideLogins { get; set; }

        /// <summary>1-based page number for the Audit Logs page.</summary>
        public int Page { get; set; } = 1;

        /// <summary>Rows per page for the Audit Logs page (10-200, default 50).</summary>
        public int PageSize { get; set; } = 50;

        /// <summary>
        /// Set by the Audit Logs controller (users holding AuditLogs.View): read every row of the organization
        /// instead of the department-scoped subset. Never set from asset audit tabs.
        /// </summary>
        public bool OrganizationWide { get; set; }
    }

    public class AuditLogVm : AuditLogDisplayItem
    {
        public int Id { get; set; }

        public string ActorUserId { get; set; }

        public string Action { get; set; }

        public string ActionLabel { get; set; }

        public string EntityType { get; set; }

        public string EntityTypeLabel { get; set; }

        public string EntityId { get; set; }

        public DateTime Timestamp { get; set; }

        public string IPAddress { get; set; }

        /// <summary>Actor shown on the Audit Logs page: name and e-mail when the actor is a known user.</summary>
        public string ActorDisplayName { get; set; }
    }

    public class AuditLogPageVm
    {
        public IList<AuditLogVm> Items { get; set; } = new List<AuditLogVm>();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages
        {
            get { return PageSize <= 0 ? 0 : (TotalCount + PageSize - 1) / PageSize; }
        }
    }
}
