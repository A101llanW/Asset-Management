using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPurchaseService
    {
        IEnumerable<PurchaseRecordVm> GetAll();

        void Create(PurchaseRecordVm model);
    }
}
