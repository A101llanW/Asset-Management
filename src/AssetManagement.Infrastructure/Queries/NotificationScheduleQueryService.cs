using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class NotificationScheduleQueryService : INotificationScheduleQueryService
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public NotificationScheduleQueryService(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<int> GetActiveOrganizationIds()
        {
            const string sql = @"
SELECT o.[Id]
FROM [Organization] o
WHERE o.[IsActive] = 1
ORDER BY o.[Id]";

            return ReadIntList(sql, null);
        }

        public IList<ScheduledAssetRow> GetExpiringWarranties(int organizationId, DateTime nowUtc, int thresholdDays)
        {
            const string sql = @"
SELECT a.[Id], a.[AssetTag], a.[WarrantyEndDate]
FROM [Asset] a
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND a.[WarrantyEndDate] IS NOT NULL
  AND a.[WarrantyEndDate] >= @NowDate
  AND a.[WarrantyEndDate] <= @ThresholdDate";

            var items = new List<ScheduledAssetRow>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@NowDate", nowUtc.Date);
                    SqlQueryHelper.AddParameter(command, "@ThresholdDate", nowUtc.Date.AddDays(thresholdDays));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new ScheduledAssetRow
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                                WarrantyEndDate = Convert.ToDateTime(reader["WarrantyEndDate"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public IList<ScheduledInsuranceRow> GetExpiringInsurance(int organizationId, DateTime nowUtc, int thresholdDays)
        {
            const string sql = @"
SELECT p.[AssetId], a.[AssetTag], p.[PolicyNumber], p.[PolicyEndDate]
FROM [InsurancePolicy] p
INNER JOIN [Asset] a ON a.[Id] = p.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND p.[IsActive] = 1
  AND p.[PolicyEndDate] >= @NowDate
  AND p.[PolicyEndDate] <= @ThresholdDate";

            var items = new List<ScheduledInsuranceRow>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@NowDate", nowUtc.Date);
                    SqlQueryHelper.AddParameter(command, "@ThresholdDate", nowUtc.Date.AddDays(thresholdDays));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new ScheduledInsuranceRow
                            {
                                AssetId = Convert.ToInt32(reader["AssetId"]),
                                AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                                PolicyNumber = SqlQueryHelper.GetString(reader, "PolicyNumber"),
                                PolicyEndDate = Convert.ToDateTime(reader["PolicyEndDate"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        public IList<ScheduledAssignmentRow> GetDueSoonAssignments(int organizationId, DateTime nowUtc, int thresholdDays)
        {
            const string sql = @"
SELECT aa.[AssetId], a.[AssetTag], aa.[ToUserId], aa.[ExpectedReturnDate], a.[CurrentStatus]
FROM [AssetAssignment] aa
INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND aa.[ExpectedReturnDate] IS NOT NULL
  AND aa.[ExpectedReturnDate] >= @NowDate
  AND aa.[ExpectedReturnDate] <= @ThresholdDate";

            return ReadAssignmentRows(sql, organizationId, nowUtc.Date, nowUtc.Date.AddDays(thresholdDays));
        }

        public IList<ScheduledAssignmentRow> GetOverdueAssignments(int organizationId, DateTime nowUtc)
        {
            const string sql = @"
SELECT aa.[AssetId], a.[AssetTag], aa.[ToUserId], aa.[ExpectedReturnDate], a.[CurrentStatus]
FROM [AssetAssignment] aa
INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND aa.[ExpectedReturnDate] IS NOT NULL
  AND aa.[ExpectedReturnDate] < @NowDate";

            return ReadAssignmentRows(sql, organizationId, nowUtc.Date, null);
        }

        public IList<ScheduledApprovalRow> GetPendingTransferApprovals(int organizationId)
        {
            const string sql = @"
SELECT t.[Id] AS EntityId, t.[AssetId], t.[RequestedById]
FROM [AssetTransfer] t
INNER JOIN [Asset] a ON a.[Id] = t.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND t.[IsActive] = 1
  AND t.[ApprovalStatus] = @PendingStatus";

            return ReadApprovalRows(sql, organizationId);
        }

        public IList<ScheduledApprovalRow> GetPendingDisposalApprovals(int organizationId)
        {
            const string sql = @"
SELECT d.[Id] AS EntityId, d.[AssetId], d.[RequestedById]
FROM [DisposalRecord] d
INNER JOIN [Asset] a ON a.[Id] = d.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND d.[IsActive] = 1
  AND d.[ApprovalStatus] = @PendingStatus";

            return ReadApprovalRows(sql, organizationId);
        }

        private IList<ScheduledAssignmentRow> ReadAssignmentRows(string sql, int organizationId, DateTime nowDate, DateTime? thresholdDate)
        {
            var items = new List<ScheduledAssignmentRow>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@NowDate", nowDate);
                    if (thresholdDate.HasValue)
                    {
                        SqlQueryHelper.AddParameter(command, "@ThresholdDate", thresholdDate.Value);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new ScheduledAssignmentRow
                            {
                                AssetId = Convert.ToInt32(reader["AssetId"]),
                                AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                                ToUserId = SqlQueryHelper.GetString(reader, "ToUserId"),
                                ExpectedReturnDate = Convert.ToDateTime(reader["ExpectedReturnDate"]),
                                AssetStatus = Convert.ToInt32(reader["CurrentStatus"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        private IList<ScheduledApprovalRow> ReadApprovalRows(string sql, int organizationId)
        {
            var items = new List<ScheduledApprovalRow>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@PendingStatus", (int)Domain.Enums.ApprovalStatus.Pending);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new ScheduledApprovalRow
                            {
                                EntityId = Convert.ToInt32(reader["EntityId"]),
                                AssetId = Convert.ToInt32(reader["AssetId"]),
                                RequestedById = SqlQueryHelper.GetString(reader, "RequestedById")
                            });
                        }
                    }
                }
            }

            return items;
        }

        private IList<int> ReadIntList(string sql, int? organizationId)
        {
            var items = new List<int>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    if (organizationId.HasValue)
                    {
                        SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId.Value);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(Convert.ToInt32(reader["Id"]));
                        }
                    }
                }
            }

            return items;
        }
    }
}
