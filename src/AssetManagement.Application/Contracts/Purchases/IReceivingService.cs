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
    }
}
