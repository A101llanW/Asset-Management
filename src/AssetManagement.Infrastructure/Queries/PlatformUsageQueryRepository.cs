using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Helpers;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// Platform usage aggregates. One grouped SELECT per metric; AuditLog login window is date-bounded.
    /// Login metric counts Action = HTTP.Account.Login only (see PlatformUsageMetrics).
    /// </summary>
    public class PlatformUsageQueryRepository : IPlatformUsageQueryRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public PlatformUsageQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<PlatformUsageOrgMetricDto> GetOrganizations()
        {
            const string sql = @"
SELECT [Id], [Name], [Slug], [IsActive]
FROM [Organization]
ORDER BY [Name];";
            var items = new List<PlatformUsageOrgMetricDto>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PlatformUsageOrgMetricDto
                            {
                                OrganizationId = Convert.ToInt32(reader["Id"]),
                                Name = Convert.ToString(reader["Name"]),
                                Slug = Convert.ToString(reader["Slug"]),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public IList<PlatformUsageCountDto> GetAssetCounts()
        {
            const string sql = @"
SELECT o.[Id] AS OrganizationId,
       COUNT(a.[Id]) AS Total,
       SUM(CASE WHEN a.[IsActive] = 1 THEN 1 ELSE 0 END) AS Active
FROM [Organization] o
LEFT JOIN [Asset] a ON a.[OrganizationId] = o.[Id]
GROUP BY o.[Id];";
            return ReadCounts(sql);
        }

        public IList<PlatformUsageCountDto> GetUserCounts()
        {
            const string sql = @"
SELECT o.[Id] AS OrganizationId,
       COUNT(u.[Id]) AS Total,
       SUM(CASE WHEN u.[IsActive] = 1 THEN 1 ELSE 0 END) AS Active
FROM [Organization] o
LEFT JOIN [Users] u ON u.[OrganizationId] = o.[Id]
GROUP BY o.[Id];";
            return ReadCounts(sql);
        }

        public IList<PlatformUsageLoginDto> GetLoginCountsSince(DateTime sinceUtc)
        {
            const string sql = @"
SELECT [OrganizationId],
       SUM(CASE WHEN [Timestamp] >= @Since24h THEN 1 ELSE 0 END) AS Last24h,
       SUM(CASE WHEN [Timestamp] >= @Since7d THEN 1 ELSE 0 END) AS Last7d,
       COUNT(*) AS Last30d
FROM [AuditLog]
WHERE [Timestamp] >= @Since30d
  AND [OrganizationId] IS NOT NULL
  AND [Action] = @LoginAction
GROUP BY [OrganizationId];";

            var items = new List<PlatformUsageLoginDto>();
            var utcNow = DateTime.UtcNow;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddParameter(command, "@Since30d", sinceUtc);
                    AddParameter(command, "@Since7d", utcNow.AddDays(-7));
                    AddParameter(command, "@Since24h", utcNow.AddHours(-24));
                    AddParameter(command, "@LoginAction", PlatformUsageMetrics.LoginActionCounted);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PlatformUsageLoginDto
                            {
                                OrganizationId = Convert.ToInt32(reader["OrganizationId"]),
                                Last24h = Convert.ToInt32(reader["Last24h"]),
                                Last7d = Convert.ToInt32(reader["Last7d"]),
                                Last30d = Convert.ToInt32(reader["Last30d"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public IList<PlatformUsageLastActivityDto> GetLastActivityByOrganization()
        {
            const string sql = @"
SELECT [OrganizationId], MAX([Timestamp]) AS LastActivityUtc
FROM [AuditLog]
WHERE [OrganizationId] IS NOT NULL
GROUP BY [OrganizationId];";
            var items = new List<PlatformUsageLastActivityDto>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PlatformUsageLastActivityDto
                            {
                                OrganizationId = Convert.ToInt32(reader["OrganizationId"]),
                                LastActivityUtc = DateTime.SpecifyKind(Convert.ToDateTime(reader["LastActivityUtc"]), DateTimeKind.Utc)
                            });
                        }
                    }
                }
            }

            return items;
        }

        public IList<PlatformUsageStorageDto> GetStorageRowCounts()
        {
            // Approximate share: sum of per-org row counts across the largest tenant-scoped tables.
            const string sql = @"
SELECT OrganizationId, SUM(RowCnt) AS [RowCount]
FROM (
    SELECT [OrganizationId], COUNT_BIG(*) AS RowCnt FROM [Asset] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
    UNION ALL
    SELECT [OrganizationId], COUNT_BIG(*) FROM [AuditLog] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
    UNION ALL
    SELECT [OrganizationId], COUNT_BIG(*) FROM [Users] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
    UNION ALL
    SELECT [OrganizationId], COUNT_BIG(*) FROM [Department] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
    UNION ALL
    SELECT [OrganizationId], COUNT_BIG(*) FROM [AssetAssignment] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
    UNION ALL
    SELECT [OrganizationId], COUNT_BIG(*) FROM [PurchaseRequest] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
    UNION ALL
    SELECT [OrganizationId], COUNT_BIG(*) FROM [LoginAttempts] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
    UNION ALL
    SELECT [OrganizationId], COUNT_BIG(*) FROM [Notification] WHERE [OrganizationId] IS NOT NULL GROUP BY [OrganizationId]
) x
GROUP BY OrganizationId;";
            var items = new List<PlatformUsageStorageDto>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PlatformUsageStorageDto
                            {
                                OrganizationId = Convert.ToInt32(reader["OrganizationId"]),
                                RowCount = Convert.ToInt64(reader["RowCount"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public PlatformUsageDatabaseSizeDto GetDatabaseSize()
        {
            const string sql = @"
SELECT
    SUM(CASE WHEN [type_desc] = N'ROWS' THEN [size] ELSE 0 END) * 8.0 / 1024.0 AS DataFileMb,
    SUM(CASE WHEN [type_desc] = N'LOG' THEN [size] ELSE 0 END) * 8.0 / 1024.0 AS LogFileMb
FROM sys.database_files;";
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new PlatformUsageDatabaseSizeDto
                            {
                                DataFileMb = Convert.ToDouble(reader["DataFileMb"]),
                                LogFileMb = Convert.ToDouble(reader["LogFileMb"])
                            };
                        }
                    }
                }
            }

            return new PlatformUsageDatabaseSizeDto();
        }

        private IList<PlatformUsageCountDto> ReadCounts(string sql)
        {
            var items = new List<PlatformUsageCountDto>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PlatformUsageCountDto
                            {
                                OrganizationId = Convert.ToInt32(reader["OrganizationId"]),
                                Total = Convert.ToInt32(reader["Total"]),
                                Active = reader["Active"] == DBNull.Value ? 0 : Convert.ToInt32(reader["Active"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
