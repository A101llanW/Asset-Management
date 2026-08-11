using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ISecurityReportExportService
    {
        byte[] ExportCsv(SecurityLogsPageVm page);

        string ExportHtml(SecurityLogsPageVm page, string applicationBaseUrl = null);
    }
}
