using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Script.Serialization;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.Helpers
{
    public static class DepartmentUserWorkflowHelper
    {
        public static string BuildUsersByDepartmentJson(IEnumerable<UserVm> users)
        {
            if (users == null)
            {
                return "{}";
            }

            var grouped = users
                .Where(x => x != null && x.IsActive)
                .GroupBy(x => x.DepartmentId.HasValue ? x.DepartmentId.Value.ToString() : string.Empty)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(u => new Dictionary<string, string>
                    {
                        { "id", u.Id },
                        { "name", BuildUserLabel(u) }
                    }).ToList());

            return new JavaScriptSerializer().Serialize(grouped);
        }

        public static string ResolveDepartmentDisplayName(int? departmentId, IEnumerable<DepartmentVm> departments)
        {
            if (!departmentId.HasValue || departments == null)
            {
                return null;
            }

            var match = departments.FirstOrDefault(x => x.Id == departmentId.Value);
            return match == null ? departmentId.Value.ToString() : match.Name;
        }

        public static string ResolveUserDisplayName(string userId, IEnumerable<UserVm> users)
        {
            if (string.IsNullOrWhiteSpace(userId) || users == null)
            {
                return null;
            }

            var match = users.FirstOrDefault(x => string.Equals(x.Id, userId, StringComparison.OrdinalIgnoreCase));
            return match == null ? userId : BuildUserLabel(match);
        }

        public static bool UserBelongsToDepartment(string userId, int? departmentId, IEnumerable<UserVm> users)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return true;
            }

            if (!departmentId.HasValue)
            {
                return true;
            }

            var user = users == null
                ? null
                : users.FirstOrDefault(x => string.Equals(x.Id, userId, StringComparison.OrdinalIgnoreCase));
            return user != null && user.DepartmentId == departmentId.Value;
        }

        public static string BuildUserLabel(UserVm user)
        {
            var name = ((user?.FirstName ?? string.Empty) + " " + (user?.LastName ?? string.Empty)).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                return user?.Email ?? user?.Id;
            }

            return string.IsNullOrWhiteSpace(user?.Email) ? name : name + " (" + user.Email + ")";
        }
    }
}
