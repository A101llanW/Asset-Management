using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class SecurityLogQueryRepository : Application.Contracts.Queries.ISecurityLogQueryRepository
    {
        private const int DefaultTake = 1000;

        private readonly ISqlConnectionFactory _connectionFactory;

        public SecurityLogQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<LoginAttemptLogVm> GetLoginAttempts(SecurityLogFilterVm filter, int? organizationId, int take)
        {
            var items = new List<LoginAttemptLogVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = BuildLoginSelectSql(false, take <= 0 ? DefaultTake : take);
                    AddLoginFilterParameters(command, filter, organizationId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(ReadLoginAttempt(reader));
                        }
                    }
                }
            }

            return items;
        }

        public int CountLoginAttempts(SecurityLogFilterVm filter, int? organizationId)
        {
            return ExecuteCount(BuildLoginSelectSql(true, 0), filter, organizationId, false);
        }

        public int CountSuccessfulLogins(SecurityLogFilterVm filter, int? organizationId)
        {
            var scopedFilter = CloneFilter(filter);
            scopedFilter.WasSuccessful = true;
            return ExecuteCount(BuildLoginSelectSql(true, 0), scopedFilter, organizationId, false);
        }

        public int CountFailedLogins(SecurityLogFilterVm filter, int? organizationId)
        {
            var scopedFilter = CloneFilter(filter);
            scopedFilter.WasSuccessful = false;
            return ExecuteCount(BuildLoginSelectSql(true, 0), scopedFilter, organizationId, false);
        }

        public IList<SecurityEventLogVm> GetSecurityEvents(SecurityLogFilterVm filter, int? organizationId, int take)
        {
            var items = new List<SecurityEventLogVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = BuildSecurityEventSelectSql(false, take <= 0 ? DefaultTake : take);
                    AddSecurityEventFilterParameters(command, filter, organizationId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new SecurityEventLogVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                EventType = Convert.ToString(reader["EventType"]),
                                Email = Convert.ToString(reader["Email"]),
                                IpAddress = Convert.ToString(reader["IpAddress"]),
                                OrganizationId = reader["OrganizationId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["OrganizationId"]),
                                CreatedAtUtc = Convert.ToDateTime(reader["CreatedAtUtc"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public int CountSecurityEvents(SecurityLogFilterVm filter, int? organizationId)
        {
            return ExecuteCount(BuildSecurityEventSelectSql(true, 0), filter, organizationId, true);
        }

        private int ExecuteCount(string sql, SecurityLogFilterVm filter, int? organizationId, bool securityEvents)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    if (securityEvents)
                    {
                        AddSecurityEventFilterParameters(command, filter, organizationId);
                    }
                    else
                    {
                        AddLoginFilterParameters(command, filter, organizationId);
                    }

                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private static string BuildLoginSelectSql(bool countOnly, int take)
        {
            var select = countOnly
                ? "SELECT COUNT(1)"
                : "SELECT TOP (" + take + ") [Id],[Username],[IpAddress],[AttemptedAtUtc],[Success],[FailureReason],[OrganizationId]";
            return select + @"
FROM [LoginAttempts]
WHERE (@OrganizationId IS NULL OR [OrganizationId] = @OrganizationId)
  AND (@Username IS NULL OR [Username] LIKE '%' + @Username + '%')
  AND (@IpAddress IS NULL OR [IpAddress] LIKE '%' + @IpAddress + '%')
  AND (@WasSuccessful IS NULL OR [Success] = @WasSuccessful)
  AND (@StartDate IS NULL OR [AttemptedAtUtc] >= @StartDate)
  AND (@EndDate IS NULL OR [AttemptedAtUtc] < @EndDate)"
                + (countOnly ? string.Empty : " ORDER BY [AttemptedAtUtc] DESC, [Id] DESC");
        }

        private static string BuildSecurityEventSelectSql(bool countOnly, int take)
        {
            var select = countOnly
                ? "SELECT COUNT(1)"
                : "SELECT TOP (" + take + ") [Id],[EventType],[Email],[IpAddress],[OrganizationId],[CreatedAtUtc]";
            return select + @"
FROM [SecurityEvents]
WHERE (@OrganizationId IS NULL OR [OrganizationId] = @OrganizationId)
  AND (@Username IS NULL OR [Email] LIKE '%' + @Username + '%')
  AND (@IpAddress IS NULL OR [IpAddress] LIKE '%' + @IpAddress + '%')
  AND (@EventType IS NULL OR [EventType] LIKE '%' + @EventType + '%')
  AND (@StartDate IS NULL OR [CreatedAtUtc] >= @StartDate)
  AND (@EndDate IS NULL OR [CreatedAtUtc] < @EndDate)"
                + (countOnly ? string.Empty : " ORDER BY [CreatedAtUtc] DESC, [Id] DESC");
        }

        private static void AddLoginFilterParameters(IDbCommand command, SecurityLogFilterVm filter, int? organizationId)
        {
            AddParameter(command, "@OrganizationId", organizationId);
            AddParameter(command, "@Username", NormalizeFilter(filter == null ? null : filter.Username));
            AddParameter(command, "@IpAddress", NormalizeFilter(filter == null ? null : filter.IpAddress));
            AddParameter(command, "@WasSuccessful", filter == null || !filter.WasSuccessful.HasValue ? (object)DBNull.Value : filter.WasSuccessful.Value);
            AddParameter(command, "@StartDate", filter == null || !filter.StartDate.HasValue ? (object)DBNull.Value : filter.StartDate.Value);
            AddParameter(command, "@EndDate", filter == null || !filter.EndDate.HasValue ? (object)DBNull.Value : filter.EndDate.Value.Date.AddDays(1));
        }

        private static void AddSecurityEventFilterParameters(IDbCommand command, SecurityLogFilterVm filter, int? organizationId)
        {
            AddParameter(command, "@OrganizationId", organizationId);
            AddParameter(command, "@Username", NormalizeFilter(filter == null ? null : filter.Username));
            AddParameter(command, "@IpAddress", NormalizeFilter(filter == null ? null : filter.IpAddress));
            AddParameter(command, "@EventType", NormalizeFilter(filter == null ? null : filter.EventType));
            AddParameter(command, "@StartDate", filter == null || !filter.StartDate.HasValue ? (object)DBNull.Value : filter.StartDate.Value);
            AddParameter(command, "@EndDate", filter == null || !filter.EndDate.HasValue ? (object)DBNull.Value : filter.EndDate.Value.Date.AddDays(1));
        }

        private static LoginAttemptLogVm ReadLoginAttempt(IDataRecord reader)
        {
            return new LoginAttemptLogVm
            {
                Id = Convert.ToInt32(reader["Id"]),
                Username = Convert.ToString(reader["Username"]),
                IpAddress = Convert.ToString(reader["IpAddress"]),
                AttemptedAtUtc = Convert.ToDateTime(reader["AttemptedAtUtc"]),
                WasSuccessful = Convert.ToBoolean(reader["Success"]),
                FailureReason = Convert.ToString(reader["FailureReason"]),
                OrganizationId = reader["OrganizationId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["OrganizationId"])
            };
        }

        private static SecurityLogFilterVm CloneFilter(SecurityLogFilterVm filter)
        {
            if (filter == null)
            {
                return new SecurityLogFilterVm();
            }

            return new SecurityLogFilterVm
            {
                Username = filter.Username,
                IpAddress = filter.IpAddress,
                WasSuccessful = filter.WasSuccessful,
                StartDate = filter.StartDate,
                EndDate = filter.EndDate,
                EventType = filter.EventType
            };
        }

        private static string NormalizeFilter(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
