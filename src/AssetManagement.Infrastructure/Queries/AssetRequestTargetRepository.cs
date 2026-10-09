using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// AssetRequest.TargetKind / TargetDepartmentId (migration 097). Plain SQL so the AssetRequest entity (and every
    /// request read/write) is untouched; when the columns are absent every request reads as a user target and room
    /// targets are refused. Writes are tenant-guarded (request and department must both be in the organization).
    /// </summary>
    public class AssetRequestTargetRepository
    {
        public const int TargetUser = 0;
        public const int TargetRoom = 1;
        private static volatile bool _columnsSeen;
        private readonly ISqlConnectionFactory _connectionFactory;

        public AssetRequestTargetRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool IsAvailable()
        {
            if (_columnsSeen)
            {
                return true;
            }

            var value = Scalar(
                "SELECT CASE WHEN COL_LENGTH(N'dbo.AssetRequest', N'TargetKind') IS NULL OR COL_LENGTH(N'dbo.AssetRequest', N'TargetDepartmentId') IS NULL THEN 0 ELSE 1 END",
                null);
            var available = value != null && value != DBNull.Value && Convert.ToInt32(value) == 1;
            if (available)
            {
                _columnsSeen = true;
            }

            return available;
        }

        /// <summary>(kind, departmentId) for the request; (0, null) when absent or not migrated.</summary>
        public void Get(int requestId, int organizationId, out int kind, out int? targetDepartmentId)
        {
            kind = TargetUser;
            targetDepartmentId = null;
            if (!IsAvailable())
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT [TargetKind], [TargetDepartmentId] FROM [AssetRequest] WHERE [Id] = @Id AND [OrganizationId] = @OrganizationId";
                    command.Parameters.Add(CreateParameter(command, "@Id", requestId));
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", organizationId));
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            kind = reader.IsDBNull(0) ? TargetUser : reader.GetInt32(0);
                            targetDepartmentId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1);
                        }
                    }
                }
            }

            if (kind != TargetRoom || !targetDepartmentId.HasValue)
            {
                kind = TargetUser;
                targetDepartmentId = null;
            }
        }

        /// <summary>Sets the target. Returns false when nothing was written (not migrated, or tenant mismatch).</summary>
        public bool Set(int requestId, int organizationId, int kind, int? targetDepartmentId)
        {
            if (kind == TargetRoom && !targetDepartmentId.HasValue)
            {
                throw new ArgumentException("A room target needs a department.", "targetDepartmentId");
            }

            if (!IsAvailable())
            {
                if (kind == TargetRoom)
                {
                    throw new InvalidOperationException("Room delivery targets are not available until database migration 097 is applied.");
                }

                return false;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE r SET r.[TargetKind] = @Kind, r.[TargetDepartmentId] = @DepartmentId
FROM [AssetRequest] r
WHERE r.[Id] = @Id AND r.[OrganizationId] = @OrganizationId
  AND (@DepartmentId IS NULL OR EXISTS (
        SELECT 1 FROM [Department] d
        WHERE d.[Id] = @DepartmentId AND d.[OrganizationId] = @OrganizationId AND d.[IsActive] = 1))";
                    command.Parameters.Add(CreateParameter(command, "@Kind", kind == TargetRoom ? TargetRoom : TargetUser));
                    command.Parameters.Add(CreateParameter(command, "@DepartmentId", kind == TargetRoom ? targetDepartmentId : null));
                    command.Parameters.Add(CreateParameter(command, "@Id", requestId));
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", organizationId));
                    return command.ExecuteNonQuery() > 0;
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
