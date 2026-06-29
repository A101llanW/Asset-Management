using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IReceivingService
    {
        PurchaseReceiveDetailVm GetReceiveDetail(int purchaseRecordId);

        ReceiveAssetLookupVm GetReceiveAssetLookup(int purchaseRecordId, int? preferredAssetId);

        int Receive(AssetReceiveVm model, string receivedById);

        IEnumerable<AssetReceivingListVm> GetReceivingsForPurchase(int purchaseRecordId);
    }
}
