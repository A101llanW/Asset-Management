using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ITransferService
    {
        TransferSubmissionResultVm Transfer(AssetTransferVm model, string requestedByUserId);

        void ApproveTransfer(TransferApprovalDecisionVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin);

        void RejectTransfer(TransferApprovalDecisionVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin);
    }
}
