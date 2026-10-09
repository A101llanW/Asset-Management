using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IReceivingService
    {
        PurchaseReceiveDetailVm GetReceiveDetail(int purchaseRecordId, bool applyCatalogMatch = false);

        ReceiveAssetLookupVm GetReceiveAssetLookup(int purchaseRecordId, int? preferredAssetId, bool applyCatalogMatch = false);

        ReceiveResultVm Receive(AssetReceiveVm model, string receivedById);

        IEnumerable<AssetReceivingListVm> GetReceivingsForPurchase(int purchaseRecordId);

        /// <summary>rel1009i: the receiving screen for a PO (lines with ordered / received / outstanding and classification).</summary>
        ReceiveOrderVm GetReceiveOrder(int purchaseRecordId);

        /// <summary>rel1009i: receive units on one or more PO lines (partial allowed, over-receipt refused, audited).</summary>
        ReceiveResultVm ReceiveOrder(ReceiveOrderVm model, string receivedById);
    }
}
