using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface ICatalogQueryRepository
    {
        PagedListVm<RoleVm> GetRoleListPage(
            int organizationId,
            string search,
            bool? isActive,
            string sort,
            string direction,
            int page,
            int pageSize);

        PagedListVm<SupplierVm> GetSupplierListPage(
            int organizationId,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize);

        PagedListVm<AssetCategoryListVm> GetAssetCategoryListPage(
            int organizationId,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize);

        PagedListVm<AssetTypeListVm> GetAssetTypeListPage(
            int organizationId,
            string search,
            int? categoryId,
            string sort,
            string direction,
            int page,
            int pageSize);
    }
}
