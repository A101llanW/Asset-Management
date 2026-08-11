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
