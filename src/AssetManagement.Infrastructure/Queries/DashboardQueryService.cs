using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class DashboardQueryService : IDashboardQueryService
    {
        private static readonly string StatusCountsSql = @"
SELECT
    COUNT(*) AS TotalAssets,
    SUM(CASE WHEN a.[CurrentStatus] = @AssignedStatus THEN 1 ELSE 0 END) AS AssignedAssets,
    SUM(CASE WHEN a.[CurrentStatus] IN (@InStoreStatus, @ReturnedStatus) THEN 1 ELSE 0 END) AS UnassignedAssets,
    SUM(CASE WHEN a.[CurrentStatus] = @UnderMaintenanceStatus THEN 1 ELSE 0 END) AS AssetsUnderMaintenance,
    SUM(CASE WHEN a.[CurrentStatus] IN (@LostStatus, @StolenStatus, @DamagedStatus) THEN 1 ELSE 0 END) AS LostDamagedStolenAssets,
    ISNULL(SUM(a.[AcquisitionCost]), 0) AS TotalAcquisitionValue
FROM [Asset] a
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a");

        private static readonly string TopDepartmentsSql = @"
SELECT TOP 5
    d.[Name] AS DepartmentName,
    ISNULL(SUM(a.[AcquisitionCost]), 0) AS BookValue,
    COUNT(*) AS AssetCount
FROM [Asset] a
INNER JOIN [Department] d ON d.[Id] = a.[DepartmentId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"
GROUP BY d.[Name]
ORDER BY SUM(a.[AcquisitionCost]) DESC, d.[Name] ASC";

        private static readonly string AssignmentsPerMonthSql = @"
SELECT
    YEAR(aa.[AssignedDate]) AS [Year],
    MONTH(aa.[AssignedDate]) AS [Month],
    COUNT(*) AS [Count]
FROM [AssetAssignment] aa
INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND aa.[AssignedDate] >= @FromDate
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"
GROUP BY YEAR(aa.[AssignedDate]), MONTH(aa.[AssignedDate])
ORDER BY YEAR(aa.[AssignedDate]), MONTH(aa.[AssignedDate])";

        private static readonly string TotalTcoSql = @"
SELECT ISNULL(SUM(a.[AcquisitionCost] + a.[TaxAmount] + ISNULL(m.[MaintenanceCost], 0)), 0)
FROM [Asset] a
LEFT JOIN (
    SELECT [AssetId], SUM([Cost]) AS MaintenanceCost
    FROM [AssetMaintenanceRecord]
    WHERE [IsActive] = 1
    GROUP BY [AssetId]
) m ON m.[AssetId] = a.[Id]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a");

        private readonly ISqlConnectionFactory _connectionFactory;

        public DashboardQueryService(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public DashboardKpisDto GetKpis(int organizationId, int? departmentId, bool bypassDepartmentScope, bool denyDepartmentScope)
        {
            if (denyDepartmentScope)
            {
                return CreateEmptyKpis();
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                var statusCounts = ReadStatusCounts(connection, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
                var topDepartments = ReadTopDepartments(connection, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
                var assignmentsPerMonth = ReadAssignmentsPerMonth(connection, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
                var totalTco = ReadTotalTco(connection, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);

                return new DashboardKpisDto
                {
                    TotalAssets = statusCounts.TotalAssets,
                    AssignedAssets = statusCounts.AssignedAssets,
                    UnassignedAssets = statusCounts.UnassignedAssets,
                    AssetsUnderMaintenance = statusCounts.AssetsUnderMaintenance,
                    LostDamagedStolenAssets = statusCounts.LostDamagedStolenAssets,
                    TotalAcquisitionValue = statusCounts.TotalAcquisitionValue,
                    TotalCostOfOwnership = totalTco,
                    TopDepartmentValues = topDepartments,
                    AssignmentsPerMonth = assignmentsPerMonth
                };
            }
        }

        private static DashboardKpisDto CreateEmptyKpis()
        {
            return new DashboardKpisDto
            {
                TotalAssets = 0,
                AssignedAssets = 0,
                UnassignedAssets = 0,
                AssetsUnderMaintenance = 0,
                LostDamagedStolenAssets = 0,
                TotalAcquisitionValue = 0m,
                TotalCostOfOwnership = 0m,
                TopDepartmentValues = new List<DepartmentValueVm>(),
                AssignmentsPerMonth = new List<DashboardTrendPointVm>()
            };
        }

        private static StatusCounts ReadStatusCounts(
            IDbConnection connection,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = StatusCountsSql;
                AddScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
                SqlQueryHelper.AddParameter(command, "@AssignedStatus", (int)AssetStatus.Assigned);
                SqlQueryHelper.AddParameter(command, "@InStoreStatus", (int)AssetStatus.InStore);
                SqlQueryHelper.AddParameter(command, "@ReturnedStatus", (int)AssetStatus.Returned);
                SqlQueryHelper.AddParameter(command, "@UnderMaintenanceStatus", (int)AssetStatus.UnderMaintenance);
                SqlQueryHelper.AddParameter(command, "@LostStatus", (int)AssetStatus.Lost);
                SqlQueryHelper.AddParameter(command, "@StolenStatus", (int)AssetStatus.Stolen);
                SqlQueryHelper.AddParameter(command, "@DamagedStatus", (int)AssetStatus.Damaged);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return new StatusCounts();
                    }

                    return new StatusCounts
                    {
                        TotalAssets = Convert.ToInt32(reader["TotalAssets"]),
                        AssignedAssets = Convert.ToInt32(reader["AssignedAssets"]),
                        UnassignedAssets = Convert.ToInt32(reader["UnassignedAssets"]),
                        AssetsUnderMaintenance = Convert.ToInt32(reader["AssetsUnderMaintenance"]),
                        LostDamagedStolenAssets = Convert.ToInt32(reader["LostDamagedStolenAssets"]),
                        TotalAcquisitionValue = Convert.ToDecimal(reader["TotalAcquisitionValue"])
                    };
                }
            }
        }

        private static IList<DepartmentValueVm> ReadTopDepartments(
            IDbConnection connection,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            var items = new List<DepartmentValueVm>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = TopDepartmentsSql;
                AddScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        items.Add(new DepartmentValueVm
                        {
                            DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                            BookValue = Convert.ToDecimal(reader["BookValue"]),
                            AssetCount = Convert.ToInt32(reader["AssetCount"])
                        });
                    }
                }
            }

            return items;
        }

        private static IList<DashboardTrendPointVm> ReadAssignmentsPerMonth(
            IDbConnection connection,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            var points = new List<DashboardTrendPointVm>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = AssignmentsPerMonthSql;
                AddScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
                SqlQueryHelper.AddParameter(command, "@FromDate", DateTime.UtcNow.AddMonths(-11));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var year = Convert.ToInt32(reader["Year"]);
                        var month = Convert.ToInt32(reader["Month"]);
                        points.Add(new DashboardTrendPointVm
                        {
                            Label = year.ToString("0000") + "-" + month.ToString("00"),
                            Count = Convert.ToInt32(reader["Count"])
                        });
                    }
                }
            }

            return points;
        }

        private static decimal ReadTotalTco(
            IDbConnection connection,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = TotalTcoSql;
                AddScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
                var result = command.ExecuteScalar();
                return result == null || result == DBNull.Value ? 0m : Convert.ToDecimal(result);
            }
        }

        private static void AddScopeParameters(
            IDbCommand command,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
        }

        private sealed class StatusCounts
        {
            public int TotalAssets { get; set; }

            public int AssignedAssets { get; set; }

            public int UnassignedAssets { get; set; }

            public int AssetsUnderMaintenance { get; set; }

            public int LostDamagedStolenAssets { get; set; }

            public decimal TotalAcquisitionValue { get; set; }
        }
    }
}
