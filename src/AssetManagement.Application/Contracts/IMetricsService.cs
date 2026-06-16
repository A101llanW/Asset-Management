using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public enum PendingApprovalCountMode
    {
        GlobalPending,
        UserInboxTotal,
        UserActionable
    }

    public interface IMetricsService
    {
        int CountDepartments(bool activeOnly = true);

        int CountAssets(AssetFilterVm filter);

        int CountPendingApprovals(PendingApprovalCountMode mode);

        int CountExpiringWarranties(int days);

        int CountExpiringInsurance(int days);

        int CountCustodyMovements(System.DateTime from, System.DateTime to);

        int GetWarrantyThresholdDays();

        int GetInsuranceThresholdDays();

        int GetMaintenanceThresholdDays();
    }
}
