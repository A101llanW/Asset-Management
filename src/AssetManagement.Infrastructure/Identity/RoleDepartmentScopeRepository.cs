using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Identity
{
    /// <summary>
    /// Roles.DepartmentId (migration 096): NULL = organization-wide role, a value = the role applies only in that
    /// department. Plain SQL so the Role entity (and every role read/write) is untouched; when the column is not
    /// there yet every role reads as organization-wide and writes of a department are refused.
    /// </summary>
    public class RoleDepartmentScopeRepository
    {
        private static volatile bool _columnSeen;
        private readonly ISqlConnectionFactory _connectionFactory;

        public RoleDepartmentScopeRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        /// <summary>True when Roles.DepartmentId exists (migration 096 applied).</summary>
        public bool IsAvailable()
        {
            if (_columnSeen)
            {
                return true;
            }

            var value = Scalar("SELECT CASE WHEN COL_LENGTH(N'dbo.Roles', N'DepartmentId') IS NULL THEN 0 ELSE 1 END", null);
            var available = value != null && value != DBNull.Value && Convert.ToInt32(value) == 1;
            if (available)
            {
                _columnSeen = true;
            }

            return available;
        }

        /// <summary>RoleId -> DepartmentId (null = organization-wide) for the organization's roles.</summary>
        public IDictionary<int, int?> GetRoleDepartmentIds(int organizationId)
        {
            var result = new Dictionary<int, int?>();
            if (!IsAvailable())
            {
                return result;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT [Id], [DepartmentId] FROM [Roles] WHERE [OrganizationId] = @OrganizationId";
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", organizationId));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result[reader.GetInt32(0)] = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>RoleId -> department name, only for the organization's department-specific roles.</summary>
        public IDictionary<int, string> GetRoleDepartmentNames(int organizationId)
        {
            var result = new Dictionary<int, string>();
            if (!IsAvailable())
            {
                return result;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT r.[Id], d.[Name]
FROM [Roles] r
INNER JOIN [Department] d ON d.[Id] = r.[DepartmentId]
WHERE r.[OrganizationId] = @OrganizationId AND r.[DepartmentId] IS NOT NULL";
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", organizationId));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result[reader.GetInt32(0)] = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>The role's department, or null when organization-wide (or the column is absent).</summary>
        public int? GetRoleDepartmentId(int roleId)
        {
            if (!IsAvailable())
            {
                return null;
            }

            var value = Scalar("SELECT [DepartmentId] FROM [Roles] WHERE [Id] = @Id", new Dictionary<string, object> { { "@Id", roleId } });
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        /// <summary>True when the department exists, is active and belongs to the organization.</summary>
        public bool IsActiveTenantDepartment(int departmentId, int organizationId)
        {
            var value = Scalar(
                "SELECT COUNT(1) FROM [Department] WHERE [Id] = @Id AND [OrganizationId] = @OrganizationId AND [IsActive] = 1",
                new Dictionary<string, object> { { "@Id", departmentId }, { "@OrganizationId", organizationId } });
            return value != null && value != DBNull.Value && Convert.ToInt32(value) > 0;
        }

        /// <summary>
        /// A role applies in a department when it is organization-wide (NULL) or tied to exactly that department.
        /// With no department chosen only organization-wide roles apply.
        /// </summary>
        public bool RoleAppliesInDepartment(int roleId, int? departmentId)
        {
            var roleDepartmentId = GetRoleDepartmentId(roleId);
            return !roleDepartmentId.HasValue || (departmentId.HasValue && roleDepartmentId.Value == departmentId.Value);
        }

        /// <summary>Sets (or clears with null) the role's department. The role must belong to the organization.</summary>
        public void SetRoleDepartmentId(int roleId, int organizationId, int? departmentId)
        {
            if (!IsAvailable())
            {
                if (departmentId.HasValue)
                {
                    throw new InvalidOperationException("Role departments are not available until database migration 096 is applied.");
                }

                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE [Roles] SET [DepartmentId] = @DepartmentId WHERE [Id] = @Id AND [OrganizationId] = @OrganizationId";
                    command.Parameters.Add(CreateParameter(command, "@DepartmentId", departmentId));
                    command.Parameters.Add(CreateParameter(command, "@Id", roleId));
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", organizationId));
                    command.ExecuteNonQuery();
                }
            }
        }

        private object Scalar(string sql, IDictionary<string, object> parameters)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    if (parameters != null)
                    {
                        foreach (var pair in parameters)
                        {
                            command.Parameters.Add(CreateParameter(command, pair.Key, pair.Value));
                        }
                    }

                    return command.ExecuteScalar();
                }
            }
        }

        private static IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }
    }
}