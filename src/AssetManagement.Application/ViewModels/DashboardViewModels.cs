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

        public int AssetsUnderMaintenance { get; set; }

        public int LostDamagedStolenAssets { get; set; }

        public decimal TotalAcquisitionValue { get; set; }

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

        public string Action { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
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
    }
}
