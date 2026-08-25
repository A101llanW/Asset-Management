using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IPermissionService
    {
        IEnumerable<PermissionVm> GetAll();

        IEnumerable<PermissionGroupVm> GetGroupedPermissions();
    }
}
