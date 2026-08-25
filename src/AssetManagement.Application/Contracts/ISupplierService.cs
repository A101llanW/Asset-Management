using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ISupplierService
    {
        IEnumerable<SupplierVm> GetAll();

        PagedListVm<SupplierVm> GetListPage(
            string search,
            string sort,
            string direction,
            int page,
            int pageSize);

        SupplierVm GetById(int id);

        int Create(SupplierVm model);

        int CreateWithCatalog(SupplierVm model, IEnumerable<SupplierCatalogItemVm> catalogItems);

        void Update(SupplierVm model);
    }
}
