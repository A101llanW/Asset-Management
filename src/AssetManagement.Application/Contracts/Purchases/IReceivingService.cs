using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IReceivingService
    {
        PurchaseReceiveDetailVm GetReceiveDetail(int purchaseRecordId);

        int Receive(AssetReceiveVm model, string receivedById);

        IEnumerable<AssetReceivingListVm> GetReceivingsForPurchase(int purchaseRecordId);
    }
}
