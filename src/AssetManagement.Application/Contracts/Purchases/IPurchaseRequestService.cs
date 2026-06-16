using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPurchaseRequestService
    {
        IEnumerable<PurchaseRequestListItemVm> GetAll();

        PurchaseRequestDetailVm GetById(int id);

        int Submit(PurchaseRequestCreateVm model, string requestedByUserId);

        void Approve(PurchaseRequestApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin);

        void Reject(PurchaseRequestApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin);
    }
}
