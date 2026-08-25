using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPendingApprovalQueryService
    {
        PendingApprovalInboxResultVm BuildInbox(PendingApprovalUserContextVm context);

        int CountGlobalPending();
    }
}
