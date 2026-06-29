using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    public static class UserRoleNameResolver
    {
        public static void ApplyOrganizationRoleNames(IEnumerable<UserVm> users, IList<RoleVm> organizationRoles)
        {
            if (users == null || organizationRoles == null)
            {
                return;
            }

            var lookup = organizationRoles
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().Name);

            foreach (var user in users)
            {
                if (user == null || !user.RoleId.HasValue)
                {
                    continue;
                }

                string roleName;
                if (lookup.TryGetValue(user.RoleId.Value, out roleName))
                {
                    user.RoleName = roleName;
                }
            }
        }
    }
}
