using System;
using System.Collections.Generic;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface INotificationScheduleQueryService
    {
        IList<int> GetActiveOrganizationIds();

        IList<ScheduledAssetRow> GetExpiringWarranties(int organizationId, DateTime nowUtc, int thresholdDays);

        IList<ScheduledInsuranceRow> GetExpiringInsurance(int organizationId, DateTime nowUtc, int thresholdDays);

        IList<ScheduledAssignmentRow> GetDueSoonAssignments(int organizationId, DateTime nowUtc, int thresholdDays);

        IList<ScheduledAssignmentRow> GetOverdueAssignments(int organizationId, DateTime nowUtc);

        IList<ScheduledApprovalRow> GetPendingTransferApprovals(int organizationId);

        IList<ScheduledApprovalRow> GetPendingDisposalApprovals(int organizationId);
    }

    public sealed class ScheduledAssetRow
    {
        public int Id { get; set; }

        public string AssetTag { get; set; }

        public DateTime WarrantyEndDate { get; set; }
    }

    public sealed class ScheduledInsuranceRow
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string PolicyNumber { get; set; }

        public DateTime PolicyEndDate { get; set; }
    }

    public sealed class ScheduledAssignmentRow
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string ToUserId { get; set; }

        public DateTime ExpectedReturnDate { get; set; }

        public int AssetStatus { get; set; }
    }

    public sealed class ScheduledApprovalRow
    {
        public int EntityId { get; set; }

        public int AssetId { get; set; }

        public string RequestedById { get; set; }
    }
}
