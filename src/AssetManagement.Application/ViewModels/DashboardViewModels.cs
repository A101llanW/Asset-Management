using System;

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

        public decimal TotalCurrentBookValue { get; set; }

        public int ExpiringWarrantyCount { get; set; }

        public int ExpiringInsuranceCount { get; set; }
    }

    public class AuditLogFilterVm
    {
        public string EntityType { get; set; }

        public string Action { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }
    }

    public class AuditLogVm
    {
        public int Id { get; set; }

        public string ActorUserId { get; set; }

        public string Action { get; set; }

        public string EntityType { get; set; }

        public string EntityId { get; set; }

        public DateTime Timestamp { get; set; }

        public string IPAddress { get; set; }
    }
}
