using System;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Security
{
    public static class DepartmentHeadAssignmentRules
    {
        public const string DepartmentHeadRoleName = "Department Head";

        public static bool IsDepartmentHeadRole(Role role)
        {
            return role != null
                && role.IsActive
                && string.Equals(role.Name, DepartmentHeadRoleName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Migration 107: a stage whose role is the Department Head role (also "Head of Department" / "HOD") resolves to the
        /// section head of the request's unit first (any person, whatever their role), then to the role holders.
        /// </summary>
        public static bool IsDepartmentHeadRoleName(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            var name = roleName.Trim();
            return string.Equals(name, DepartmentHeadRoleName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Head of Department", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "HOD", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDepartmentHeadRoleId(int? roleId, Func<int, Role> roleResolver)
        {
            if (!roleId.HasValue || roleResolver == null)
            {
                return false;
            }

            return IsDepartmentHeadRole(roleResolver(roleId.Value));
        }
    }
}
