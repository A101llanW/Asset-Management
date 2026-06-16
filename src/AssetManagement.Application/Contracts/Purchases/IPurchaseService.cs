using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPurchaseService
    {
        IEnumerable<PurchaseRecordVm> GetAll();

        PurchaseRecordVm GetById(int id);

        int Create(PurchaseRecordVm model);
    }
}
