using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPurchaseRequestService
    {
        IEnumerable<PurchaseRequestListItemVm> GetAll();

        PagedListVm<PurchaseRequestListItemVm> GetListPage(
            string search,
            string sort,
            string direction,
            int page,
            int pageSize);

        PurchaseRequestDetailVm GetById(int id);

        int Submit(PurchaseRequestCreateVm model, string requestedByUserId);

        void SaveAttachment(int purchaseRequestId, PurchaseRequestAttachmentInfo attachment);

        string GetAttachmentRelativePath(int purchaseRequestId);

        void Approve(PurchaseRequestApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin);

        void Reject(PurchaseRequestApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin);
    }
}
