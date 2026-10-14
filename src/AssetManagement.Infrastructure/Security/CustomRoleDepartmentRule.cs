using System;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Security
{
    /// <summary>
    /// A user holding a custom (non-system) tenant role must have a department when the organization has
    /// active departments; otherwise department scoping leaves them with no assets at all. Used by every
    /// create / assign path (MembershipService.CreateUser, invitations, UserService.AssignRole/AssignDepartment).
    /// </summary>
    public static class CustomRoleDepartmentRule
    {
        public const string Message =
            "A department is required for users with a custom role. Choose a department (only Company Admin and other system roles may be left without one).";

        private const string RequiresDepartmentSql = @"
SELECT CASE WHEN r.[IsSystemRole] = 0 AND r.[OrganizationId] IS NOT NULL
             AND EXISTS (SELECT 1 FROM [Department] d WHERE d.[OrganizationId] = r.[OrganizationId] AND d.[IsActive] = 1)
            THEN 1 ELSE 0 END
FROM [Roles] r WHERE r.[Id] = @RoleId";

        public static bool RoleRequiresDepartment(ISqlConnectionFactory connectionFactory, int? roleId)
        {
            if (connectionFactory == null || !roleId.HasValue)
            {
                return false;
            }

            using (var connection = connectionFactory.CreateConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = RequiresDepartmentSql;
                command.Parameters.Add("@RoleId", SqlDbType.Int).Value = roleId.Value;
                if (connection.State != ConnectionState.Open)
                {
                    connection.Open();
                }

                var value = command.ExecuteScalar();
                return value != null && value != DBNull.Value && Convert.ToInt32(value) == 1;
            }
        }

        /// <summary>Returns the error message, or null when the combination is allowed.</summary>
        public static string Validate(ISqlConnectionFactory connectionFactory, int? roleId, int? departmentId)
        {
            return !departmentId.HasValue && RoleRequiresDepartment(connectionFactory, roleId) ? Message : null;
        }
    }
}
