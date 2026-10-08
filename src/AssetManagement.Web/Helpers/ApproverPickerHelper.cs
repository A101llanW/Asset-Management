using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using AssetManagement.Application.Services;
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

        /// <summary>
        /// Per-role active users for the optional stage "Approver (person)" / PO-raiser person picker.
        /// Input must already be org-scoped (tenant-filtered user list); inactive users are excluded.
        /// Label = email, plus name when available.
        /// </summary>
        public static IDictionary<int, IList<SelectListItem>> BuildStagePersonOptions(IEnumerable<RoleVm> roles, IEnumerable<UserVm> users)
        {
            var roleIds = new HashSet<int>((roles ?? Enumerable.Empty<RoleVm>()).Where(x => x != null && x.Id > 0).Select(x => x.Id));
            var result = new Dictionary<int, IList<SelectListItem>>();
            foreach (var group in (users ?? Enumerable.Empty<UserVm>())
                .Where(x => x != null && x.IsActive && !string.IsNullOrWhiteSpace(x.Id) && x.RoleId.HasValue && roleIds.Contains(x.RoleId.Value))
                .GroupBy(x => x.RoleId.Value))
            {
                result[group.Key] = group
                    .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.First())
                    .Select(x => new SelectListItem
                    {
                        Value = x.Id,
                        Text = StageApproverRules.BuildPersonLabel(x.Email, x.FirstName, x.LastName, x.Id)
                    })
                    .OrderBy(x => x.Text, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            return result;
        }

        public static string SerializeStagePersonOptions(IDictionary<int, IList<SelectListItem>> options)
        {
            var payload = (options ?? new Dictionary<int, IList<SelectListItem>>())
                .OrderBy(x => x.Key)
                .Select(x => new
                {
                    roleId = x.Key,
                    users = x.Value.Select(u => new { id = u.Value, label = u.Text }).ToList()
                })
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
