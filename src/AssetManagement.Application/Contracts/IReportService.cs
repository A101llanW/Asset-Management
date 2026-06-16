using System;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IReportService
    {
        DashboardVm GetDashboard();

        ReportsHubVm GetReportsHub();

        ReportDocumentResultVm GenerateReportDocument(ReportExportRequestVm request, string generatedBy);

        byte[] ExportAssetRegisterCsv();

        byte[] ExportCustodyMovementCsv(DateTime? fromDate, DateTime? toDate);

        byte[] ExportDepartmentSummaryCsv();

        byte[] ExportPendingApprovalsAgingCsv();

        byte[] ExportGeneralLedgerCsv();
    }
}
