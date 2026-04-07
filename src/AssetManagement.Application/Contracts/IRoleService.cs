using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IRoleService
    {
        IEnumerable<RoleVm> GetRoles();

        RoleVm GetById(int id);

        void Create(RoleCreateEditVm model);

        void Update(RoleCreateEditVm model);

        void SetPermissions(int roleId, IEnumerable<int> permissionIds);
    }
}
