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

        /// <summary>Returns the first created requisition id (see <see cref="SubmitBatch"/> for split submits).</summary>
        int Submit(PurchaseRequestCreateVm model, string requestedByUserId);

        /// <summary>Creates one requisition per distinct approval path (MixedFlowPolicy AutoSplit) or one as before.</summary>
        PurchaseRequestSubmitResult SubmitBatch(PurchaseRequestCreateVm model, string requestedByUserId);

        /// <summary>Create form preview: per-line approval path using the same classification and resolution as submit.</summary>
        PurchaseRequestPathPreviewVm PreviewPaths(PurchaseRequestCreateVm model);

        /// <summary>Requisitions auto-split from one form; null when the group is unknown or none is visible to the user.</summary>
        PurchaseRequestBatchVm GetBatch(int groupId);

        void SaveAttachment(int purchaseRequestId, PurchaseRequestAttachmentInfo attachment);

        string GetAttachmentRelativePath(int purchaseRequestId);

        void Approve(PurchaseRequestApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin);

        void Reject(PurchaseRequestApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin);
    }
}
