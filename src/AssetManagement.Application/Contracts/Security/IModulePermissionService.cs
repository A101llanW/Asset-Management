using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
namespace AssetManagement.Application.Contracts.Security
{
    public interface IModulePermissionService
    {
        IList<PermissionGroupVm> GetModuleGroupedPermissions();

        IList<PermissionGroupVm> GetModuleGroupedPermissions(IEnumerable<int> permissionIds);

        bool UserHasModuleAccess(string userId, string moduleKey, IAuthorizationService authorizationService);
    }
}
