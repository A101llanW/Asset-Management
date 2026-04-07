using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IReportService
    {
        DashboardVm GetDashboard();
    }
}
