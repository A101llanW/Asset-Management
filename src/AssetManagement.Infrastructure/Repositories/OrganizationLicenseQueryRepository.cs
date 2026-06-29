using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.ViewModels.Organizations;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Repositories
{
    public class OrganizationLicenseQueryRepository : IOrganizationLicenseQueryRepository
    {
        private const string CountSql = @"
SELECT COUNT(*)
FROM [OrganizationLicense] ol
INNER JOIN [Organization] o ON o.[Id] = ol.[OrganizationId]
WHERE ol.[IsActive] = 1
  AND o.[IsActive] = 1
  AND (@Search IS NULL OR o.[Name] LIKE @Search OR o.[Slug] LIKE @Search)
  AND (@Status IS NULL OR ol.[Status] = @Status)
  AND (@ExpiringWithinDays IS NULL OR (
        ol.[ExpiryDate] >= GETUTCDATE()
        AND ol.[ExpiryDate] <= DATEADD(DAY, @ExpiringWithinDays, GETUTCDATE())
        AND ol.[Status] IN (N'Active', N'PendingRenewal')
      ))";

        private const string PageSqlTemplate = @"
SELECT
    ol.[Id] AS LicenseId,
    ol.[OrganizationId],
    o.[Name] AS OrganizationName,
    o.[Slug] AS OrganizationSlug,
    ol.[Status],
    ol.[StartDate],
    ol.[ExpiryDate],
    ol.[MaxUsers]
FROM [OrganizationLicense] ol
INNER JOIN [Organization] o ON o.[Id] = ol.[OrganizationId]
WHERE ol.[IsActive] = 1
  AND o.[IsActive] = 1
  AND (@Search IS NULL OR o.[Name] LIKE @Search OR o.[Slug] LIKE @Search)
  AND (@Status IS NULL OR ol.[Status] = @Status)
  AND (@ExpiringWithinDays IS NULL OR (
        ol.[ExpiryDate] >= GETUTCDATE()
        AND ol.[ExpiryDate] <= DATEADD(DAY, @ExpiringWithinDays, GETUTCDATE())
        AND ol.[Status] IN (N'Active', N'PendingRenewal')
      ))
ORDER BY {0}
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

        private const string ExpiryCandidatesSql = @"
SELECT
    ol.[Id] AS LicenseId,
    ol.[OrganizationId],
    ol.[Status],
    ol.[ExpiryDate]
FROM [OrganizationLicense] ol
WHERE ol.[IsActive] = 1
  AND ol.[ExpiryDate] < GETUTCDATE()
  AND ol.[Status] IN (N'Active', N'PendingRenewal')";

        private const string HistorySql = @"
SELECT
    h.[Id],
    h.[Action],
    h.[PreviousExpiryDate],
    h.[NewExpiryDate],
    h.[PreviousStatus],
    h.[NewStatus],
    h.[PerformedBy],
    h.[Reason],
    h.[CreatedAt]
FROM [OrganizationLicenseHistory] h
WHERE h.[OrganizationId] = @OrganizationId
ORDER BY h.[CreatedAt] DESC, h.[Id] DESC";

        private readonly ISqlConnectionFactory _connectionFactory;

        public OrganizationLicenseQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public int CountLicenses(LicenseListFilterVm filter)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = CountSql;
                    AddFilterParameters(command, filter);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public IList<LicenseListItemVm> GetLicensePage(LicenseListFilterVm filter, string sort, string direction, int skip, int take)
        {
            var orderBy = BuildOrderBy(sort, direction);
            var sql = string.Format(PageSqlTemplate, orderBy);
            var items = new List<LicenseListItemVm>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddFilterParameters(command, filter);
                    AddParameter(command, "@Skip", skip);
                    AddParameter(command, "@Take", take);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var expiryDate = Convert.ToDateTime(reader["ExpiryDate"]);
                            var status = GetString(reader, "Status");
                            items.Add(new LicenseListItemVm
                            {
                                LicenseId = Convert.ToInt32(reader["LicenseId"]),
                                OrganizationId = Convert.ToInt32(reader["OrganizationId"]),
                                OrganizationName = GetString(reader, "OrganizationName"),
                                OrganizationSlug = GetString(reader, "OrganizationSlug"),
                                Status = status,
                                EffectiveStatus = ComputeEffectiveStatus(status, expiryDate),
                                StartDate = Convert.ToDateTime(reader["StartDate"]),
                                ExpiryDate = expiryDate,
                                DaysRemaining = ComputeDaysRemaining(expiryDate),
                                MaxUsers = reader["MaxUsers"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["MaxUsers"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public IList<LicenseExpiryCandidateVm> GetLicensesDueForExpiry()
        {
            var items = new List<LicenseExpiryCandidateVm>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = ExpiryCandidatesSql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new LicenseExpiryCandidateVm
                            {
                                LicenseId = Convert.ToInt32(reader["LicenseId"]),
                                OrganizationId = Convert.ToInt32(reader["OrganizationId"]),
                                Status = GetString(reader, "Status"),
                                ExpiryDate = Convert.ToDateTime(reader["ExpiryDate"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public IList<LicenseHistoryItemVm> GetHistoryForOrganization(int organizationId)
        {
            var items = new List<LicenseHistoryItemVm>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = HistorySql;
                    AddParameter(command, "@OrganizationId", organizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new LicenseHistoryItemVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Action = GetString(reader, "Action"),
                                PreviousExpiryDate = reader["PreviousExpiryDate"] == DBNull.Value
                                    ? (DateTime?)null
                                    : Convert.ToDateTime(reader["PreviousExpiryDate"]),
                                NewExpiryDate = reader["NewExpiryDate"] == DBNull.Value
                                    ? (DateTime?)null
                                    : Convert.ToDateTime(reader["NewExpiryDate"]),
                                PreviousStatus = GetString(reader, "PreviousStatus"),
                                NewStatus = GetString(reader, "NewStatus"),
                                PerformedBy = GetString(reader, "PerformedBy"),
                                Reason = GetString(reader, "Reason"),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        private static string BuildOrderBy(string sort, string direction)
        {
            var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sort ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "organization":
                    return desc ? "o.[Name] DESC, ol.[Id] DESC" : "o.[Name] ASC, ol.[Id] ASC";
                case "status":
                    return desc ? "ol.[Status] DESC, ol.[Id] DESC" : "ol.[Status] ASC, ol.[Id] ASC";
                case "start":
                    return desc ? "ol.[StartDate] DESC, ol.[Id] DESC" : "ol.[StartDate] ASC, ol.[Id] ASC";
                case "expiry":
                    return desc ? "ol.[ExpiryDate] DESC, ol.[Id] DESC" : "ol.[ExpiryDate] ASC, ol.[Id] ASC";
                default:
                    return desc ? "ol.[ExpiryDate] DESC, o.[Name] DESC" : "ol.[ExpiryDate] ASC, o.[Name] ASC";
            }
        }

        private static void AddFilterParameters(IDbCommand command, LicenseListFilterVm filter)
        {
            var search = string.IsNullOrWhiteSpace(filter == null ? null : filter.Search)
                ? null
                : "%" + filter.Search.Trim() + "%";
            AddParameter(command, "@Search", search ?? (object)DBNull.Value);
            AddParameter(command, "@Status",
                string.IsNullOrWhiteSpace(filter == null ? null : filter.Status)
                    ? (object)DBNull.Value
                    : filter.Status.Trim());
            AddIntParameter(command, "@ExpiringWithinDays",
                filter != null ? filter.ExpiringWithinDays : null);
        }

        private static LicenseStatus ComputeEffectiveStatus(string status, DateTime expiryDate)
        {
            if (string.Equals(status, LicenseStatus.Paused.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return LicenseStatus.Paused;
            }

            if (string.Equals(status, LicenseStatus.Expired.ToString(), StringComparison.OrdinalIgnoreCase)
                || expiryDate < DateTime.UtcNow)
            {
                return LicenseStatus.Expired;
            }

            if (string.Equals(status, LicenseStatus.PendingRenewal.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return LicenseStatus.PendingRenewal;
            }

            if (expiryDate <= DateTime.UtcNow.AddDays(30))
            {
                return LicenseStatus.PendingRenewal;
            }

            return LicenseStatus.Active;
        }

        private static int ComputeDaysRemaining(DateTime expiryDate)
        {
            return (int)Math.Ceiling((expiryDate - DateTime.UtcNow).TotalDays);
        }

        private static string GetString(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? null : value.ToString();
        }

        private static void AddIntParameter(IDbCommand command, string name, int? value)
        {
            var parameter = new SqlParameter(name, SqlDbType.Int)
            {
                Value = value.HasValue ? (object)value.Value : DBNull.Value
            };
            command.Parameters.Add(parameter);
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
