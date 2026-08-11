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
    }
}
