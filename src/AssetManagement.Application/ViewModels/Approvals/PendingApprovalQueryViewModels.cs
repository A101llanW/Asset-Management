using System;
using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    public class PendingApprovalUserContextVm
    {
        public string UserId { get; set; }

        public int? RoleId { get; set; }

        public bool IsSuperAdmin { get; set; }

        public bool CanApproveAssetRequests { get; set; }
    }

    public class PendingApprovalQueryItemVm
    {
        public string ProcessName { get; set; }

        public int RequestId { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string RequestedById { get; set; }

        public DateTime RequestedDateUtc { get; set; }

        public int StageNumber { get; set; }

        public string StageRoleName { get; set; }

        public bool CanCurrentUserAct { get; set; }

        public bool RequestedByCurrentUser { get; set; }

        public string Summary { get; set; }

        public int AgeDays { get; set; }

        public string AgingBand { get; set; }
    }

    public class PendingApprovalInboxResultVm
    {
        public IList<PendingApprovalQueryItemVm> Items { get; set; }

        public int TotalCount { get; set; }

        public int ActionableCount { get; set; }

        public int RequestedByMeCount { get; set; }
    }
}
