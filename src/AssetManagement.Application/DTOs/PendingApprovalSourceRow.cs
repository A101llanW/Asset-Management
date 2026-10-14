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

        /// <summary>Display label for RequestedById (name or email), resolved org-scoped; null if unknown.</summary>
        public string RequestedByName { get; set; }

        public DateTime RequestedDateUtc { get; set; }

        public int CurrentApprovalStage { get; set; }

        public string ApprovalStageRoleIds { get; set; }

        public string ApprovalStageUserIds { get; set; }

        public string Summary { get; set; }

        public string DisplayTag { get; set; }

        public string DisplayName { get; set; }

        public int? DepartmentId { get; set; }

        public bool IsAssetRequest { get; set; }

        /// <summary>Requisitions only: stored winning approval source (catalog paths resolve approvers path-first).</summary>
        public string ApprovalFlowSource { get; set; }
    }
}
