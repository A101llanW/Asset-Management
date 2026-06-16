using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class AuditLogQueryRepository : IAuditLogQueryRepository
    {
        private const string SelectSql = @"
SELECT
    al.[Id],
    al.[ActorUserId],
    al.[Action],
    al.[EntityType],
    al.[EntityId],
    al.[Timestamp],
    al.[IPAddress]
FROM [AuditLog] al
WHERE al.[OrganizationId] = @OrganizationId
  AND (@EntityType IS NULL OR al.[EntityType] = @EntityType)
  AND (@Action IS NULL OR al.[Action] = @Action)
  AND (@FromDate IS NULL OR al.[Timestamp] >= @FromDate)
  AND (@ToDate IS NULL OR al.[Timestamp] <= @ToDate)
  AND (
        @RelatedAssetId IS NULL
        OR (
            (al.[EntityType] = N'Asset' AND al.[EntityId] = CAST(@RelatedAssetId AS nvarchar(50)))
            OR al.[NewValues] = CAST(@RelatedAssetId AS nvarchar(50))
            OR (al.[EntityType] = N'AssetAssignment' AND EXISTS (
                SELECT 1 FROM [AssetAssignment] aa
                WHERE aa.[OrganizationId] = @OrganizationId
                  AND CAST(aa.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND aa.[AssetId] = @RelatedAssetId))
            OR (al.[EntityType] = N'AssetTransfer' AND EXISTS (
                SELECT 1 FROM [AssetTransfer] t
                WHERE t.[OrganizationId] = @OrganizationId
                  AND CAST(t.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND t.[AssetId] = @RelatedAssetId))
            OR (al.[EntityType] = N'AssetIncident' AND EXISTS (
                SELECT 1 FROM [AssetIncident] i
                WHERE i.[OrganizationId] = @OrganizationId
                  AND CAST(i.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND i.[AssetId] = @RelatedAssetId))
            OR (al.[EntityType] = N'AssetMaintenanceRecord' AND EXISTS (
                SELECT 1 FROM [AssetMaintenanceRecord] m
                WHERE m.[OrganizationId] = @OrganizationId
                  AND CAST(m.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND m.[AssetId] = @RelatedAssetId))
            OR (al.[EntityType] = N'AssetReturn' AND EXISTS (
                SELECT 1 FROM [AssetReturn] r
                WHERE r.[OrganizationId] = @OrganizationId
                  AND CAST(r.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND r.[AssetId] = @RelatedAssetId))
            OR (al.[EntityType] = N'InsuranceClaim' AND EXISTS (
                SELECT 1 FROM [InsuranceClaim] c
                WHERE c.[OrganizationId] = @OrganizationId
                  AND CAST(c.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND c.[AssetId] = @RelatedAssetId))
            OR (al.[EntityType] = N'AssetDocument' AND EXISTS (
                SELECT 1 FROM [AssetDocument] d
                INNER JOIN [Asset] a ON a.[Id] = d.[AssetId]
                WHERE a.[OrganizationId] = @OrganizationId
                  AND CAST(d.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND d.[AssetId] = @RelatedAssetId))
            OR (al.[EntityType] = N'DisposalRecord' AND EXISTS (
                SELECT 1 FROM [DisposalRecord] dr
                WHERE dr.[OrganizationId] = @OrganizationId
                  AND CAST(dr.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND dr.[AssetId] = @RelatedAssetId))
        )
      )
  AND (
        @BypassDepartmentScope = 1
        OR al.[ActorUserId] = @ActorUserId
        OR (
            @RelatedAssetId IS NOT NULL
            AND EXISTS (
                SELECT 1
                FROM [Asset] a
                WHERE a.[OrganizationId] = @OrganizationId
                  AND a.[IsActive] = 1
                  AND a.[Id] = @RelatedAssetId
                  AND (@BypassDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND a.[DepartmentId] = @DepartmentId))
            )
        )
        OR (
            @RelatedAssetId IS NULL
            AND (
                @DenyDepartmentScope = 0
                OR (
                    al.[EntityType] = N'Asset'
                    AND EXISTS (
                        SELECT 1
                        FROM [Asset] a
                        WHERE a.[OrganizationId] = @OrganizationId
                          AND a.[IsActive] = 1
                          AND CAST(a.[Id] AS nvarchar(50)) = al.[EntityId]
                          AND a.[DepartmentId] = @DepartmentId
                    )
                )
            )
        )
      )
ORDER BY al.[Timestamp] DESC, al.[Id] DESC";

        private readonly ISqlConnectionFactory _connectionFactory;

        public AuditLogQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<AuditLogVm> GetLogs(
            AuditLogFilterVm filter,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string actorUserId)
        {
            var items = new List<AuditLogVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = SelectSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
                    SqlQueryHelper.AddParameter(command, "@ActorUserId",
                        string.IsNullOrWhiteSpace(actorUserId) ? (object)DBNull.Value : actorUserId.Trim());
                    AddFilterParameters(command, filter);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new AuditLogVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                ActorUserId = SqlQueryHelper.GetString(reader, "ActorUserId"),
                                Action = SqlQueryHelper.GetString(reader, "Action"),
                                EntityType = SqlQueryHelper.GetString(reader, "EntityType"),
                                EntityId = SqlQueryHelper.GetString(reader, "EntityId"),
                                Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                                IPAddress = SqlQueryHelper.GetString(reader, "IPAddress")
                            });
                        }
                    }
                }
            }

            return items;
        }

        private static void AddFilterParameters(IDbCommand command, AuditLogFilterVm filter)
        {
            SqlQueryHelper.AddParameter(command, "@EntityType",
                filter == null || string.IsNullOrWhiteSpace(filter.EntityType) ? (object)DBNull.Value : filter.EntityType.Trim());
            SqlQueryHelper.AddParameter(command, "@Action",
                filter == null || string.IsNullOrWhiteSpace(filter.Action) ? (object)DBNull.Value : filter.Action.Trim());
            SqlQueryHelper.AddParameter(command, "@FromDate",
                filter == null || !filter.FromDate.HasValue ? (object)DBNull.Value : filter.FromDate.Value);
            SqlQueryHelper.AddParameter(command, "@ToDate",
                filter == null || !filter.ToDate.HasValue ? (object)DBNull.Value : filter.ToDate.Value);
            SqlQueryHelper.AddParameter(command, "@RelatedAssetId",
                filter == null || !filter.RelatedAssetId.HasValue ? (object)DBNull.Value : filter.RelatedAssetId.Value);
        }
    }
}
