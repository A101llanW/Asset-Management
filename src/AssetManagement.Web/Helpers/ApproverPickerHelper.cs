using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.Helpers
{
    public static class ApproverPickerHelper
    {
        public static string BuildRoleUsersJson(IEnumerable<RoleVm> roles, IEnumerable<UserVm> users)
        {
            var activeUsers = (users ?? Enumerable.Empty<UserVm>())
                .Where(x => x != null && x.IsActive && !string.IsNullOrWhiteSpace(x.Id))
                .ToList();

            var payload = (roles ?? Enumerable.Empty<RoleVm>())
                .Where(x => x != null && x.Id > 0)
                .OrderBy(x => x.Name)
                .Select(role => new
                {
                    roleId = role.Id,
                    roleName = role.Name,
                    users = activeUsers
                        .Where(user => user.RoleId.HasValue && user.RoleId.Value == role.Id)
                        .OrderBy(user => DepartmentUserWorkflowHelper.BuildUserLabel(user))
                        .Select(user => new
                        {
                            id = user.Id,
                            name = DepartmentUserWorkflowHelper.BuildUserLabel(user)
                        })
                        .ToList()
                })
                .Where(group => group.users.Count > 0)
                .ToList();

            return new JavaScriptSerializer().Serialize(payload);
        }

        public static IDictionary<string, string> BuildUserNameLookup(IEnumerable<UserVm> users)
        {
            var lookup = new Dictionary<string, string>();
            foreach (var user in users ?? Enumerable.Empty<UserVm>())
            {
                if (user == null || string.IsNullOrWhiteSpace(user.Id) || lookup.ContainsKey(user.Id))
                {
                    continue;
                }

                lookup[user.Id] = DepartmentUserWorkflowHelper.BuildUserLabel(user);
            }

            return lookup;
        }
    }
}
