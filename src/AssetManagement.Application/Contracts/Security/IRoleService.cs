using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IRoleService
    {
        IEnumerable<RoleVm> GetRoles();

        PagedListVm<RoleVm> GetListPage(
            string search,
            bool? isActive,
            string sort,
            string direction,
            int page,
            int pageSize);

        RoleVm GetById(int id);

        IList<int> GetPermissionIds(int roleId);

        int Create(RoleCreateEditVm model);

        void Update(RoleCreateEditVm model);

        void SetPermissions(int roleId, IEnumerable<int> permissionIds);
    }
}
