using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class ModulePermissionService : IModulePermissionService
    {
        private readonly IPermissionService _permissionService;

        public ModulePermissionService(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        public IList<PermissionGroupVm> GetModuleGroupedPermissions()
        {
            return GroupPermissions(_permissionService.GetAll());
        }

        public IList<PermissionGroupVm> GetModuleGroupedPermissions(IEnumerable<int> permissionIds)
        {
            var idSet = new HashSet<int>(permissionIds ?? Enumerable.Empty<int>());
            if (idSet.Count == 0)
            {
                return new List<PermissionGroupVm>();
            }

            return GroupPermissions(_permissionService.GetAll().Where(p => idSet.Contains(p.Id)));
        }

        public bool UserHasModuleAccess(string userId, string moduleKey, IAuthorizationService authorizationService)
        {
            if (authorizationService == null || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(moduleKey))
            {
                return false;
            }

            foreach (var code in ModulePermissionCatalog.PermissionCodesForModule(moduleKey))
            {
                if (authorizationService.HasPermission(userId, code))
                {
                    return true;
                }
            }

            return false;
        }

        private static IList<PermissionGroupVm> GroupPermissions(IEnumerable<PermissionVm> permissions)
        {
            var allPermissions = (permissions ?? Enumerable.Empty<PermissionVm>()).ToList();
            var grouped = new List<PermissionGroupVm>();
            var assigned = new HashSet<int>();

            foreach (var module in ModulePermissionCatalog.All)
            {
                var codes = new HashSet<string>(module.PermissionCodes);
                var modulePermissions = allPermissions
                    .Where(p => codes.Contains(p.Code))
                    .OrderBy(p => p.Code)
                    .ToList();

                foreach (var permission in modulePermissions)
                {
                    assigned.Add(permission.Id);
                }

                if (modulePermissions.Count == 0)
                {
                    continue;
                }

                grouped.Add(new PermissionGroupVm
                {
                    Module = module.DisplayName,
                    ModuleDescription = module.Description,
                    Permissions = modulePermissions
                });
            }

            var remainder = allPermissions
                .Where(p => !assigned.Contains(p.Id))
                .OrderBy(p => p.Code)
                .ToList();

            if (remainder.Count > 0)
            {
                grouped.Add(new PermissionGroupVm
                {
                    Module = "Other",
                    ModuleDescription = "Additional permissions not mapped to a standard module group.",
                    Permissions = remainder
                });
            }

            return grouped;
        }
    }
}
