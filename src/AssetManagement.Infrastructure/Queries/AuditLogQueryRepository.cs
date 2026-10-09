using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// Audit log reads. Both statements are shaped to run without a query memory grant
    /// (no SQL sort/hash): ids are located first through seekable branches, full rows are
    /// fetched by primary key with nested loops, and ordering is done in memory unless a
    /// row cap is requested, in which case IX_AuditLog_Org_Timestamp_Id supplies the order.
    /// </summary>
    public class AuditLogQueryRepository : IAuditLogQueryRepository
    {
        /// <summary>Asset history must fail fast so Assets/Details can render without it.</summary>
        private const int RelatedAssetCommandTimeoutSeconds = 15;

        private const string FilterSql = @"
  AND (@EntityType IS NULL OR al.[EntityType] = @EntityType)
  AND (@Action IS NULL OR al.[Action] = @Action)
  AND (@FromDate IS NULL OR al.[Timestamp] >= @FromDate)
  AND (@ToDate IS NULL OR al.[Timestamp] < @ToDate)";

        // Org-wide list (no related asset). {0} = optional TOP, {1} = optional ORDER BY.
        private const string OrganizationLogsSql = @"
SELECT {0}
    al.[Id],
    al.[ActorUserId],
    al.[Action],
    al.[EntityType],
    al.[EntityId],
    al.[Timestamp],
    al.[IPAddress]
FROM [AuditLog] al
WHERE al.[OrganizationId] = @OrganizationId" + FilterSql + @"
  AND (
        @BypassDepartmentScope = 1
        OR al.[ActorUserId] = @ActorUserId
        OR @DenyDepartmentScope = 0
        OR (
            al.[EntityType] = N'Asset'
            AND EXISTS (
                SELECT 1
                FROM [Asset] a
                WHERE a.[Id] = TRY_CAST(al.[EntityId] AS int)
                  AND CAST(a.[Id] AS nvarchar(50)) = al.[EntityId]
                  AND a.[OrganizationId] = @OrganizationId
                  AND a.[IsActive] = 1
                  AND a.[DepartmentId] = @DepartmentId
            )
        )
      )
{1}
OPTION (LOOP JOIN, MAXDOP 1)";

        // Asset history: candidate ids from seekable branches (UNION ALL, duplicates removed in
        // memory), then full rows by primary key. Same matching rules as the previous single query.
        private const string RelatedAssetLogsSql = @"
SELECT
    al.[Id],
    al.[ActorUserId],
    al.[Action],
    al.[EntityType],
    al.[EntityId],
    al.[Timestamp],
    al.[IPAddress]
FROM (
    SELECT al0.[Id]
    FROM [AuditLog] al0
    WHERE al0.[OrganizationId] = @OrganizationId
      AND al0.[EntityType] IN (N'Asset', N'Assets')
      AND (
            al0.[EntityId] = CAST(@RelatedAssetId AS nvarchar(50))
            OR al0.[EntityId] LIKE CAST(@RelatedAssetId AS nvarchar(50)) + N',%'
            OR al0.[EntityId] LIKE N'%,' + CAST(@RelatedAssetId AS nvarchar(50)) + N',%'
            OR al0.[EntityId] LIKE N'%,' + CAST(@RelatedAssetId AS nvarchar(50)))
    UNION ALL
    SELECT al0.[Id]
    FROM [AuditLog] al0
    WHERE al0.[OrganizationId] = @OrganizationId
      AND (
            al0.[NewValues] = CAST(@RelatedAssetId AS nvarchar(50))
            OR al0.[NewValues] LIKE CAST(@RelatedAssetId AS nvarchar(50)) + N'|%'
            OR al0.[NewValues] LIKE N'%|' + CAST(@RelatedAssetId AS nvarchar(50)))
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetAssignment] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetAssignment' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetTransfer] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetTransfer' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetIncident] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetIncident' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetMaintenanceRecord] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetMaintenanceRecord' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetReturn] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetReturn' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [InsuranceClaim] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'InsuranceClaim' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetDocument] x
    INNER JOIN [Asset] a ON a.[Id] = x.[AssetId] AND a.[OrganizationId] = @OrganizationId
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetDocument' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [DisposalRecord] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'DisposalRecord' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [InsurancePolicy] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'InsurancePolicy' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId AND x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetRequest] x
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetRequest' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[OrganizationId] = @OrganizationId
      AND (x.[FulfilledAssetId] = @RelatedAssetId OR x.[RequestedAssetId] = @RelatedAssetId)
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetReceiving] x
    INNER JOIN [Asset] a ON a.[Id] = x.[AssetId] AND a.[OrganizationId] = @OrganizationId
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetReceiving' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[AssetId] = @RelatedAssetId
    UNION ALL
    SELECT al0.[Id]
    FROM [AssetDocumentRequirement] x
    INNER JOIN [Asset] a ON a.[Id] = x.[AssetId] AND a.[OrganizationId] = @OrganizationId
    INNER JOIN [AuditLog] al0 ON al0.[OrganizationId] = @OrganizationId
        AND al0.[EntityType] = N'AssetDocumentRequirement' AND al0.[EntityId] = CAST(x.[Id] AS nvarchar(50))
    WHERE x.[AssetId] = @RelatedAssetId
) ids
INNER JOIN [AuditLog] al ON al.[Id] = ids.[Id]
WHERE al.[OrganizationId] = @OrganizationId" + FilterSql + @"
  AND (
        @BypassDepartmentScope = 1
        OR al.[ActorUserId] = @ActorUserId
        OR EXISTS (
            SELECT 1
            FROM [Asset] a
            WHERE a.[Id] = @RelatedAssetId
              AND a.[OrganizationId] = @OrganizationId
              AND a.[IsActive] = 1
              AND (@BypassDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND a.[DepartmentId] = @DepartmentId))
        )
      )
OPTION (LOOP JOIN, MAXDOP 1)";

        // Audit Logs page (rel1009g): AuditLog rows (HTTP GET page views hidden unless @IncludePageViews = 1)
        // merged with LoginAttempts sign-in rows (negative ids), actor resolved from Users, paged in SQL.
        private const string PageSourceSql = @"
WITH src AS (
    SELECT al.[Id], al.[ActorUserId], al.[Action], al.[EntityType], al.[EntityId], al.[Timestamp], al.[IPAddress]
    FROM [AuditLog] al
    WHERE al.[OrganizationId] = @OrganizationId" + FilterSql + @"
      AND (@IncludePageViews = 1 OR NOT (al.[Action] LIKE N'HTTP.%' AND al.[NewValues] LIKE N'VIEW:%'))
      AND (
            @BypassDepartmentScope = 1
            OR al.[ActorUserId] = @ActorUserId
            OR @DenyDepartmentScope = 0
            OR (
                al.[EntityType] = N'Asset'
                AND EXISTS (
                    SELECT 1
                    FROM [Asset] a
                    WHERE a.[Id] = TRY_CAST(al.[EntityId] AS int)
                      AND a.[OrganizationId] = @OrganizationId
                      AND a.[IsActive] = 1
                      AND a.[DepartmentId] = @DepartmentId
                )
            )
          )
    UNION ALL
    SELECT -la.[Id], la.[Username],
           CASE WHEN la.[Success] = 1 THEN N'Account.LoginSucceeded' ELSE N'Account.LoginFailed' END,
           N'Account', la.[FailureReason], la.[AttemptedAtUtc], la.[IpAddress]
    FROM [LoginAttempts] la
    WHERE @IncludeLogins = 1
      AND @BypassDepartmentScope = 1
      AND la.[OrganizationId] = @OrganizationId
      AND (@EntityType IS NULL OR @EntityType = N'Account')
      AND (@Action IS NULL OR @Action = CASE WHEN la.[Success] = 1 THEN N'Account.LoginSucceeded' ELSE N'Account.LoginFailed' END)
      AND (@FromDate IS NULL OR la.[AttemptedAtUtc] >= @FromDate)
      AND (@ToDate IS NULL OR la.[AttemptedAtUtc] < @ToDate)
), rows AS (
    SELECT src.[Id], src.[ActorUserId], src.[Action], src.[EntityType], src.[EntityId], src.[Timestamp], src.[IPAddress],
           u.[FirstName], u.[LastName], u.[Email]
    FROM src
    LEFT JOIN [Users] u ON u.[Id] = src.[ActorUserId]
    WHERE @ActorLike IS NULL
       OR src.[ActorUserId] LIKE @ActorLike
       OR u.[Email] LIKE @ActorLike
       OR (ISNULL(u.[FirstName], N'') + N' ' + ISNULL(u.[LastName], N'')) LIKE @ActorLike
)";

        private const string PageSql = PageSourceSql + @"
SELECT COUNT(*) FROM rows;
" + PageSourceSql + @"
SELECT [Id], [ActorUserId], [Action], [EntityType], [EntityId], [Timestamp], [IPAddress], [FirstName], [LastName], [Email]
FROM rows
ORDER BY [Timestamp] DESC, [Id] DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

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
            var relatedAssetId = filter == null ? null : filter.RelatedAssetId;
            var maxRows = filter != null && filter.MaxRows.HasValue && filter.MaxRows.Value > 0
                ? filter.MaxRows
                : null;
            var byId = new Dictionary<int, AuditLogVm>();
            var ordered = new List<AuditLogVm>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    if (relatedAssetId.HasValue)
                    {
                        command.CommandText = RelatedAssetLogsSql;
                        command.CommandTimeout = RelatedAssetCommandTimeoutSeconds;
                    }
                    else
                    {
                        // TOP n reads the newest rows straight off IX_AuditLog_Org_Timestamp_Id; an
                        // uncapped list is ordered in memory so the plan needs no sort grant.
                        command.CommandText = string.Format(
                            OrganizationLogsSql,
                            maxRows.HasValue ? "TOP (@MaxRows)" : string.Empty,
                            maxRows.HasValue ? "ORDER BY al.[Timestamp] DESC, al.[Id] DESC" : string.Empty);
                        if (maxRows.HasValue)
                        {
                            SqlQueryHelper.AddParameter(command, "@MaxRows", maxRows.Value);
                        }
                    }

                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
                    SqlQueryHelper.AddParameter(command, "@ActorUserId",
                        string.IsNullOrWhiteSpace(actorUserId) ? (object)DBNull.Value : actorUserId.Trim());
                    AddFilterParameters(command, filter);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["Id"]);
                            if (byId.ContainsKey(id))
                            {
                                continue;
                            }

                            var item = new AuditLogVm
                            {
                                Id = id,
                                ActorUserId = SqlQueryHelper.GetString(reader, "ActorUserId"),
                                Action = SqlQueryHelper.GetString(reader, "Action"),
                                EntityType = SqlQueryHelper.GetString(reader, "EntityType"),
                                EntityId = SqlQueryHelper.GetString(reader, "EntityId"),
                                Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                                IPAddress = SqlQueryHelper.GetString(reader, "IPAddress")
                            };
                            byId.Add(id, item);
                            ordered.Add(item);
                        }
                    }
                }
            }

            if (maxRows.HasValue && !relatedAssetId.HasValue)
            {
                return ordered;
            }

            return ordered
                .OrderByDescending(x => x.Timestamp)
                .ThenByDescending(x => x.Id)
                .ToList();
        }

        public AuditLogPageVm GetLogPage(
            AuditLogFilterVm filter,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string actorUserId,
            int skip,
            int take)
        {
            filter = filter ?? new AuditLogFilterVm();
            var result = new AuditLogPageVm();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = PageSql;
                    command.CommandTimeout = 60;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
                    SqlQueryHelper.AddParameter(command, "@ActorUserId",
                        string.IsNullOrWhiteSpace(actorUserId) ? (object)DBNull.Value : actorUserId.Trim());
                    AddFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@IncludePageViews", filter.IncludePageViews ? 1 : 0);
                    SqlQueryHelper.AddParameter(command, "@IncludeLogins", filter.HideLogins ? 0 : 1);
                    SqlQueryHelper.AddParameter(command, "@ActorLike",
                        string.IsNullOrWhiteSpace(filter.Actor) ? (object)DBNull.Value : "%" + filter.Actor.Trim() + "%");
                    SqlQueryHelper.AddParameter(command, "@Skip", skip < 0 ? 0 : skip);
                    SqlQueryHelper.AddParameter(command, "@Take", take <= 0 ? AuditLogFilterHelper.DefaultPageSize : take);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result.TotalCount = Convert.ToInt32(reader[0]);
                        }

                        if (reader.NextResult())
                        {
                            while (reader.Read())
                            {
                                var actor = SqlQueryHelper.GetString(reader, "ActorUserId");
                                result.Items.Add(new AuditLogVm
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    ActorUserId = actor,
                                    Action = SqlQueryHelper.GetString(reader, "Action"),
                                    EntityType = SqlQueryHelper.GetString(reader, "EntityType"),
                                    EntityId = SqlQueryHelper.GetString(reader, "EntityId"),
                                    Timestamp = Convert.ToDateTime(reader["Timestamp"]),
                                    IPAddress = SqlQueryHelper.GetString(reader, "IPAddress"),
                                    ActorDisplayName = AuditLogFilterHelper.FormatActor(
                                        actor,
                                        SqlQueryHelper.GetString(reader, "FirstName"),
                                        SqlQueryHelper.GetString(reader, "LastName"),
                                        SqlQueryHelper.GetString(reader, "Email"))
                                });
                            }
                        }
                    }
                }
            }

            return result;
        }

        private static void AddFilterParameters(IDbCommand command, AuditLogFilterVm filter)
        {
            SqlQueryHelper.AddParameter(command, "@EntityType",
                filter == null || string.IsNullOrWhiteSpace(filter.EntityType) ? (object)DBNull.Value : filter.EntityType.Trim());
            SqlQueryHelper.AddParameter(command, "@Action",
                filter == null || string.IsNullOrWhiteSpace(filter.Action) ? (object)DBNull.Value : filter.Action.Trim());
            SqlQueryHelper.AddParameter(command, "@FromDate",
                filter == null || !filter.FromDate.HasValue
                    ? (object)DBNull.Value
                    : KenyaTimeHelper.StartOfLocalDayToUtc(filter.FromDate.Value));
            SqlQueryHelper.AddParameter(command, "@ToDate",
                filter == null || !filter.ToDate.HasValue
                    ? (object)DBNull.Value
                    : KenyaTimeHelper.ExclusiveEndOfLocalDayToUtc(filter.ToDate.Value));
            SqlQueryHelper.AddParameter(command, "@RelatedAssetId",
                filter == null || !filter.RelatedAssetId.HasValue ? (object)DBNull.Value : filter.RelatedAssetId.Value);
        }
    }
}
