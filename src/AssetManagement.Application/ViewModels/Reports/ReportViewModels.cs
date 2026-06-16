using System;
using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class ReportsHubVm
    {
        public DashboardVm Dashboard { get; set; }

        public int AssetRegisterCount { get; set; }

        public int CustodyMovementCount { get; set; }

        public int DepartmentCount { get; set; }

        public int PendingApprovalCount { get; set; }

        public IList<ReportDefinitionVm> ReportDefinitions { get; set; } = new List<ReportDefinitionVm>();

        public IList<ReportLookupVm> Departments { get; set; } = new List<ReportLookupVm>();

        public IList<ReportLookupVm> Categories { get; set; } = new List<ReportLookupVm>();
    }

    public class ReportDefinitionVm
    {
        public string Key { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Section { get; set; }

        public string ThemeColor { get; set; }

        public bool SupportsDepartmentFilter { get; set; }

        public bool SupportsDateRange { get; set; }

        public bool SupportsCategoryFilter { get; set; }

        public bool SupportsStatusFilter { get; set; }

        public string DefaultPeriodPreset { get; set; }

        public bool UsesForwardPeriods { get; set; }
    }

    public class ReportLookupVm
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    public class ReportExportRequestVm
    {
        public string ReportType { get; set; }

        public string PeriodPreset { get; set; }

        public DateTime? FromDate { get; set; }

        public DateTime? ToDate { get; set; }

        public int? DepartmentId { get; set; }

        public int? CategoryId { get; set; }

        public AssetStatus? Status { get; set; }

        public string SortBy { get; set; }

        public string SortDirection { get; set; }
    }

    public class ReportDocumentResultVm
    {
        public string Html { get; set; }

        public byte[] CsvBytes { get; set; }

        public int RowCount { get; set; }

        public string FileName { get; set; }

        public string Title { get; set; }
    }

    public class NotificationInboxVm
    {
        public int Id { get; set; }

        public string UserId { get; set; }

        public bool IsPersonal { get; set; }

        public string Type { get; set; }

        public string Subject { get; set; }

        public string Message { get; set; }

        public string Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public string LinkUrl { get; set; }
    }
}
