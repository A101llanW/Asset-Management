using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ISupplierService
    {
        IEnumerable<SupplierVm> GetAll();

        SupplierVm GetById(int id);

        int Create(SupplierVm model);

        int CreateWithCatalog(SupplierVm model, IEnumerable<SupplierCatalogItemVm> catalogItems);

        void Update(SupplierVm model);
    }
}
