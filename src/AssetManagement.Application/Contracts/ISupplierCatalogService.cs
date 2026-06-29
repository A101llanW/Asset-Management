using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ISupplierCatalogService
    {
        IEnumerable<SupplierCatalogItemVm> GetBySupplier(int supplierId);

        SupplierCatalogItemVm GetById(int id);

        int Create(SupplierCatalogItemVm model);

        void Update(SupplierCatalogItemVm model);

        void Deactivate(int id);

        void AddItemsForSupplier(int supplierId, IEnumerable<SupplierCatalogItemVm> items);

        SupplierPriceComparisonResultVm GetPriceComparison(int? purchaseRequestId, string itemDescription, int? categoryId = null);
    }
}
