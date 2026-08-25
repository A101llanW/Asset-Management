using System;

namespace AssetManagement.Application.DTOs
{
    public class PendingApprovalSourceRow
    {
        public string ProcessName { get; set; }

        public int RequestId { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string RequestedById { get; set; }

        public DateTime RequestedDateUtc { get; set; }

        public int CurrentApprovalStage { get; set; }

        public string ApprovalStageRoleIds { get; set; }

        public string ApprovalStageUserIds { get; set; }

        public string Summary { get; set; }

        public string DisplayTag { get; set; }

        public string DisplayName { get; set; }

        public int? DepartmentId { get; set; }

        public bool IsAssetRequest { get; set; }
    }
}
