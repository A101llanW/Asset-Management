using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPurchaseService
    {
        IEnumerable<PurchaseRecordVm> GetAll();

        PagedListVm<PurchaseRecordVm> GetListPage(
            string search,
            int? supplierId,
            string sort,
            string direction,
            int page,
            int pageSize);

        PurchaseRecordVm GetById(int id);

        int Create(PurchaseRecordVm model);

        /// <summary>
        /// Create with the acting user: when linked to a requisition whose flow has a "Purchase order raised by"
        /// configured, only that person / role may create it (no Company Admin exemption: admins change the setting).
        /// </summary>
        int Create(PurchaseRecordVm model, string actingUserId, int? actingRoleId);

        /// <summary>rel1009i: PO draft prefilled from an approved requisition's lines (null when not approved).</summary>
        PurchaseRecordVm BuildFromRequisition(int purchaseRequestId);

        /// <summary>rel1009i: approved requisitions among the visible ids that still have quantity to order.</summary>
        IList<PurchaseRequisitionOptionVm> GetRequisitionsReadyForPurchaseOrder(IEnumerable<int> visibleRequestIds);

        /// <summary>rel1009i: ordered / received / outstanding per line and the PO status.</summary>
        PurchaseOrderStatusVm GetOrderStatus(int purchaseRecordId);

        /// <summary>rel1009i: the POs raised from a requisition and its fulfilment.</summary>
        RequisitionOrderSummaryVm GetRequisitionOrderSummary(int purchaseRequestId);
    }
}
