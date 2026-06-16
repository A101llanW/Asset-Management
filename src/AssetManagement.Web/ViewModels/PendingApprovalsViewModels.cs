using System.Collections.Generic;
using System;

namespace AssetManagement.Web.ViewModels
{
    public class PendingApprovalInboxContext
    {
        public ICollection<PendingApprovalItemViewModel> Items { get; set; }

        public IDictionary<int, string> Roles { get; set; }

        public IDictionary<int, AssetManagement.Domain.Entities.Asset> Assets { get; set; }

        public string CurrentUserId { get; set; }

        public int? CurrentRoleId { get; set; }

        public bool IsSuperAdmin { get; set; }
    }

    public class PendingApprovalSource
    {
        public string StageRoleIds { get; set; }

        public int CurrentStage { get; set; }

        public string RequestedById { get; set; }

        public DateTime RequestedDateUtc { get; set; }

        public string ProcessName { get; set; }

        public int RequestId { get; set; }

        public int AssetId { get; set; }

        public string Summary { get; set; }

        public string DetailsUrl { get; set; }

        public string DisplayTag { get; set; }

        public string DisplayName { get; set; }
    }

    public class PendingApprovalItemViewModel
    {
        public string ProcessName { get; set; }

        public int RequestId { get; set; }

        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string RequestedById { get; set; }

        public string RequestedDateText { get; set; }

        public DateTime RequestedDateUtc { get; set; }

        public int StageNumber { get; set; }

        public string StageRoleName { get; set; }

        public bool CanCurrentUserAct { get; set; }

        public bool RequestedByCurrentUser { get; set; }

        public string Summary { get; set; }

        public string DetailsUrl { get; set; }

        public int AgeDays { get; set; }

        public string AgingBand { get; set; }

        public string AgingBadgeClass { get; set; }
    }

    public class PendingApprovalsIndexViewModel
    {
        public IEnumerable<PendingApprovalItemViewModel> Items { get; set; } = new List<PendingApprovalItemViewModel>();

        public int TotalPendingCount { get; set; }

        public int ActionableCount { get; set; }

        public int RequestedByMeCount { get; set; }

        public string ProcessFilter { get; set; }

        public int? MinAgeDays { get; set; }

        public bool ActionableOnly { get; set; }
    }
}
