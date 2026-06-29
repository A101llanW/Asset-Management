using System;
using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels.Organizations
{
    public class LicenseListFilterVm
    {
        public string Search { get; set; }

        public string Status { get; set; }

        public int? ExpiringWithinDays { get; set; }
    }

    public class LicenseListItemVm
    {
        public int LicenseId { get; set; }

        public int OrganizationId { get; set; }

        public string OrganizationName { get; set; }

        public string OrganizationSlug { get; set; }

        public string Status { get; set; }

        public LicenseStatus EffectiveStatus { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public int DaysRemaining { get; set; }

        public int? MaxUsers { get; set; }
    }

    public class LicenseListPageVm
    {
        public IList<LicenseListItemVm> Items { get; set; }

        public int TotalCount { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; }

        public LicenseListFilterVm Filter { get; set; }
    }

    public class LicenseHistoryItemVm
    {
        public int Id { get; set; }

        public string Action { get; set; }

        public DateTime? PreviousExpiryDate { get; set; }

        public DateTime? NewExpiryDate { get; set; }

        public string PreviousStatus { get; set; }

        public string NewStatus { get; set; }

        public string PerformedBy { get; set; }

        public string Reason { get; set; }

        public DateTime CreatedAt { get; set; }
    }

    public class OrganizationLicenseDetailVm
    {
        public int LicenseId { get; set; }

        public int OrganizationId { get; set; }

        public string OrganizationName { get; set; }

        public string OrganizationSlug { get; set; }

        public string Status { get; set; }

        public LicenseStatus EffectiveStatus { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public int DaysRemaining { get; set; }

        public int? MaxUsers { get; set; }

        public DateTime? PausedAt { get; set; }

        public string PausedBy { get; set; }

        public string PauseReason { get; set; }

        public string Notes { get; set; }

        public IList<LicenseHistoryItemVm> History { get; set; }
    }

    public class RenewLicenseRequest
    {
        public int OrganizationId { get; set; }

        public DateTime NewExpiryDate { get; set; }

        public string Notes { get; set; }
    }

    public class PauseLicenseRequest
    {
        public int OrganizationId { get; set; }

        public string Reason { get; set; }
    }

    public class ResumeLicenseRequest
    {
        public int OrganizationId { get; set; }

        public string Notes { get; set; }
    }

    public class UpdateLicenseLimitsRequest
    {
        public int OrganizationId { get; set; }

        public int? MaxUsers { get; set; }

        public string Notes { get; set; }
    }

    public class LicenseOperationResult
    {
        public bool Succeeded { get; set; }

        public string Message { get; set; }
    }
}
