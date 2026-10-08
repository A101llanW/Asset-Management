using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// Tenant Dashboard KPIs. Every statement here is shaped to need NO query memory grant
    /// (no hash join/aggregate, no sort): SQL Express on small hosts can shrink query memory
    /// to a few MB, and grant-hungry statements then wait on RESOURCE_SEMAPHORE until the
    /// 30s command timeout fires. Aggregates read the narrow covering index
    /// IX_Asset_Org_Active_Dept_DashboardKpi (migration 086); small lookups use LOOP JOIN;
    /// final grouping/ordering of tiny result sets happens in memory.
    /// </summary>
    public class DashboardQueryService : IDashboardQueryService
    {
        private static readonly string StatusCountsSql = @"
SELECT
    COUNT(*) AS TotalAssets,
    ISNULL(SUM(CASE WHEN a.[CurrentStatus] = @AssignedStatus THEN 1 ELSE 0 END), 0) AS AssignedAssets,
    ISNULL(SUM(CASE WHEN a.[CurrentStatus] IN (@InStoreStatus, @ReturnedStatus) THEN 1 ELSE 0 END), 0) AS UnassignedAssets,
    ISNULL(SUM(CASE WHEN a.[CurrentStatus] IN (@UnderMaintenanceStatus, @DamagedStatus) THEN 1 ELSE 0 END), 0) AS MaintenanceAndDamagedAssets,
    ISNULL(SUM(CASE WHEN a.[CurrentStatus] IN (@LostStatus, @StolenStatus) THEN 1 ELSE 0 END), 0) AS LostAssets,
    ISNULL(SUM(CASE WHEN a.[CurrentStatus] IN (@LostStatus, @StolenStatus, @DamagedStatus) THEN 1 ELSE 0 END), 0) AS LostDamagedStolenAssets,
    ISNULL(SUM(a.[AcquisitionCost]), 0) AS TotalAcquisitionValue,
    ISNULL(SUM(a.[CurrentBookValue]), 0) AS TotalCurrentBookValue,
    ISNULL(SUM(a.[AccumulatedDepreciation]), 0) AS TotalAccumulatedDepreciation,
    ISNULL(SUM(a.[AcquisitionCost] + a.[TaxAmount]), 0) AS TotalAcquisitionAndTax,
    ISNULL(AVG(
        CASE
            WHEN a.[DepreciationRatePercent] IS NOT NULL AND a.[DepreciationRatePercent] > 0 THEN a.[DepreciationRatePercent]
            WHEN at.[DepreciationRatePercent] IS NOT NULL AND at.[DepreciationRatePercent] > 0 THEN at.[DepreciationRatePercent]
            WHEN ac.[DefaultDepreciationRatePercent] IS NOT NULL AND ac.[DefaultDepreciationRatePercent] > 0 THEN ac.[DefaultDepreciationRatePercent]
            WHEN COALESCE(NULLIF(a.[DepreciationLifeMonths], 0), NULLIF(at.[DepreciationLifeMonths], 0), NULLIF(ac.[DefaultDepreciationLifeMonths], 0), NULLIF(a.[UsefulLifeMonths], 0), NULLIF(at.[UsefulLifeMonths], 0), NULLIF(ac.[DefaultUsefulLifeMonths], 0)) IS NOT NULL
                THEN 1200.0 / COALESCE(NULLIF(a.[DepreciationLifeMonths], 0), NULLIF(at.[DepreciationLifeMonths], 0), NULLIF(ac.[DefaultDepreciationLifeMonths], 0), NULLIF(a.[UsefulLifeMonths], 0), NULLIF(at.[UsefulLifeMonths], 0), NULLIF(ac.[DefaultUsefulLifeMonths], 0))
            ELSE NULL
        END
    ), 0) AS AverageAnnualDepreciationRatePercent
FROM [Asset] a
LEFT JOIN [AssetType] at ON at.[Id] = a.[AssetTypeId]
LEFT JOIN [AssetCategory] ac ON ac.[Id] = a.[CategoryId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"
OPTION (LOOP JOIN)";

        // Per-department aggregates (stream aggregate in index order), names joined after.
        // Grouping by name + TOP 5 ordering is done in memory on these few rows.
        private static readonly string DepartmentValuesSql = @"
SELECT
    d.[Name] AS DepartmentName,
    g.[BookValue],
    g.[AcquisitionValue],
    g.[AssetCount]
FROM (
    SELECT
        a.[DepartmentId],
        SUM(a.[CurrentBookValue]) AS BookValue,
        SUM(a.[AcquisitionCost]) AS AcquisitionValue,
        COUNT(*) AS AssetCount
    FROM [Asset] a
    WHERE a.[OrganizationId] = @OrganizationId
      AND a.[IsActive] = 1
      AND a.[DepartmentId] IS NOT NULL
      AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"
    GROUP BY a.[DepartmentId]
) g
INNER JOIN [Department] d ON d.[Id] = g.[DepartmentId]
OPTION (LOOP JOIN, ORDER GROUP)";

        // Raw assignment dates for the trend window; bucketed by month in memory.
        private static readonly string AssignmentDatesSql = @"
SELECT aa.[AssignedDate]
FROM [AssetAssignment] aa
INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND aa.[AssignedDate] >= @FromDate
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"
OPTION (LOOP JOIN)";

        // TCO = SUM(AcquisitionCost + TaxAmount) (from StatusCountsSql) + active maintenance cost
        // of the same scoped assets. Both cost columns are NOT NULL, so this equals the old
        // per-asset LEFT JOIN formulation without its hash aggregate.
        private static readonly string MaintenanceCostSql = @"
SELECT ISNULL(SUM(m.[Cost]), 0)
FROM [AssetMaintenanceRecord] m
INNER JOIN [Asset] a ON a.[Id] = m.[AssetId]
WHERE m.[IsActive] = 1
  AND a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"
OPTION (LOOP JOIN)";

        // Replaces MetricsService.CountExpiringWarranties/CountExpiringInsurance on the dashboard,
        // which materialised every org asset (SELECT *) to count in memory.
        private static readonly string ExpiryCountsSql = @"
SELECT
    (SELECT COUNT(*)
     FROM [Asset] a
     WHERE a.[OrganizationId] = @OrganizationId
       AND a.[IsActive] = 1
       AND a.[WarrantyEndDate] IS NOT NULL
       AND a.[WarrantyEndDate] <= @WarrantyThreshold
       AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @") AS ExpiringWarrantyCount,
    (SELECT COUNT(*)
     FROM [InsurancePolicy] p
     WHERE p.[OrganizationId] = @OrganizationId
       AND p.[PolicyEndDate] <= @InsuranceThreshold
       AND EXISTS (
           SELECT 1
           FROM [Asset] a
           WHERE a.[Id] = p.[AssetId]
             AND a.[OrganizationId] = @OrganizationId
             AND a.[IsActive] = 1
             AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @")) AS ExpiringInsuranceCount
OPTION (LOOP JOIN)";

        // Reports hub custody count. The old in-memory version loaded every org asset (SELECT *) and
        // every AssetTransfer/AssetAssignment row of all orgs on each Reports/Index hit.
        // Asset scope has no IsActive filter, matching ApplyAssetScope over Repository<Asset>.Query().
        private static readonly string CustodyMovementsSql = @"
SELECT
    (SELECT COUNT(*)
     FROM [AssetTransfer] t
     WHERE t.[TransferDate] >= @FromDate
       AND t.[TransferDate] < @ToDateExclusive
       AND EXISTS (
           SELECT 1
           FROM [Asset] a
           WHERE a.[Id] = t.[AssetId]
             AND a.[OrganizationId] = @OrganizationId
             AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @"))
  + (SELECT COUNT(*)
     FROM [AssetAssignment] aa
     WHERE aa.[AssignedDate] >= @FromDate
       AND aa.[AssignedDate] < @ToDateExclusive
       AND EXISTS (
           SELECT 1
           FROM [Asset] a
           WHERE a.[Id] = aa.[AssetId]
             AND a.[OrganizationId] = @OrganizationId
             AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @")) AS CustodyMovements
OPTION (LOOP JOIN, MAXDOP 1)";

        private const int TopDepartmentCount = 5;

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
                var maintenanceCost = ReadMaintenanceCost(connection, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);

                return new DashboardKpisDto
                {
                    TotalAssets = statusCounts.TotalAssets,
                    AssignedAssets = statusCounts.AssignedAssets,
                    UnassignedAssets = statusCounts.UnassignedAssets,
                    MaintenanceAndDamagedAssets = statusCounts.MaintenanceAndDamagedAssets,
                    LostAssets = statusCounts.LostAssets,
                    LostDamagedStolenAssets = statusCounts.LostDamagedStolenAssets,
                    TotalAcquisitionValue = statusCounts.TotalAcquisitionValue,
                    TotalCurrentBookValue = statusCounts.TotalCurrentBookValue,
                    TotalAccumulatedDepreciation = statusCounts.TotalAccumulatedDepreciation,
                    AverageAnnualDepreciationRatePercent = statusCounts.AverageAnnualDepreciationRatePercent,
                    TotalCostOfOwnership = statusCounts.TotalAcquisitionAndTax + maintenanceCost,
                    TopDepartmentValues = topDepartments,
                    AssignmentsPerMonth = assignmentsPerMonth
                };
            }
        }

        public DashboardExpiryCountsDto GetExpiryCounts(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool includeClassDepartmentAssets,
            DateTime warrantyThresholdUtc,
            DateTime insuranceThresholdUtc)
        {
            if (denyDepartmentScope)
            {
                return new DashboardExpiryCountsDto();
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = ExpiryCountsSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(
                        command,
                        bypassDepartmentScope,
                        denyDepartmentScope,
                        departmentId,
                        includeClassDepartmentAssets && !bypassDepartmentScope);
                    SqlQueryHelper.AddParameter(command, "@WarrantyThreshold", warrantyThresholdUtc);
                    SqlQueryHelper.AddParameter(command, "@InsuranceThreshold", insuranceThresholdUtc);

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return new DashboardExpiryCountsDto();
                        }

                        return new DashboardExpiryCountsDto
                        {
                            ExpiringWarrantyCount = ReadInt32(reader, "ExpiringWarrantyCount"),
                            ExpiringInsuranceCount = ReadInt32(reader, "ExpiringInsuranceCount")
                        };
                    }
                }
            }
        }

        public int CountCustodyMovements(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool includeClassDepartmentAssets,
            DateTime fromDate,
            DateTime toDate)
        {
            if (denyDepartmentScope)
            {
                return 0;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = CustodyMovementsSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(
                        command,
                        bypassDepartmentScope,
                        denyDepartmentScope,
                        departmentId,
                        includeClassDepartmentAssets && !bypassDepartmentScope);
                    // Old rule compared x.Date >= from.Date && x.Date <= to.Date (whole days).
                    SqlQueryHelper.AddParameter(command, "@FromDate", fromDate.Date);
                    SqlQueryHelper.AddParameter(command, "@ToDateExclusive", toDate.Date.AddDays(1));
                    var value = command.ExecuteScalar();
                    return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
                }
            }
        }

        public IList<ReportAssetRefDto> GetAssetRefs(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool includeClassDepartmentAssets,
            IEnumerable<int> assetIds)
        {
            var result = new List<ReportAssetRefDto>();
            var ids = (assetIds ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return result;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                for (var offset = 0; offset < ids.Count; offset += AssetRefChunkSize)
                {
                    var chunk = ids.Skip(offset).Take(AssetRefChunkSize).ToList();
                    using (var command = connection.CreateCommand())
                    {
                        var names = new List<string>();
                        for (var i = 0; i < chunk.Count; i++)
                        {
                            names.Add("@AssetId" + i);
                            SqlQueryHelper.AddParameter(command, "@AssetId" + i, chunk[i]);
                        }

                        command.CommandText = @"
SELECT a.[Id], a.[AssetTag], a.[AssetName], a.[DepartmentId],
       CASE WHEN " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + @" THEN 1 ELSE 0 END AS InScope
FROM [Asset] a
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[Id] IN (" + string.Join(", ", names) + @")
OPTION (MAXDOP 1)";
                        SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                        SqlQueryHelper.AddDepartmentScopeParameters(
                            command,
                            bypassDepartmentScope,
                            denyDepartmentScope,
                            departmentId,
                            includeClassDepartmentAssets && !bypassDepartmentScope);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Add(new ReportAssetRefDto
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                                    AssetName = SqlQueryHelper.GetString(reader, "AssetName"),
                                    DepartmentId = reader["DepartmentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["DepartmentId"]),
                                    InScope = Convert.ToInt32(reader["InScope"]) == 1
                                });
                            }
                        }
                    }
                }
            }

            return result;
        }

        public IDictionary<string, string> GetUserLabels(int organizationId, IEnumerable<string> keys)
        {
            return UserLabelLookup.Resolve(_connectionFactory, organizationId, keys);
        }

        private const int AssetRefChunkSize = 500;

        private static DashboardKpisDto CreateEmptyKpis()
        {
            return new DashboardKpisDto
            {
                TotalAssets = 0,
                AssignedAssets = 0,
                UnassignedAssets = 0,
                MaintenanceAndDamagedAssets = 0,
                LostAssets = 0,
                LostDamagedStolenAssets = 0,
                TotalAcquisitionValue = 0m,
                TotalCurrentBookValue = 0m,
                TotalAccumulatedDepreciation = 0m,
                AverageAnnualDepreciationRatePercent = 0m,
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
                        TotalAssets = ReadInt32(reader, "TotalAssets"),
                        AssignedAssets = ReadInt32(reader, "AssignedAssets"),
                        UnassignedAssets = ReadInt32(reader, "UnassignedAssets"),
                        MaintenanceAndDamagedAssets = ReadInt32(reader, "MaintenanceAndDamagedAssets"),
                        LostAssets = ReadInt32(reader, "LostAssets"),
                        LostDamagedStolenAssets = ReadInt32(reader, "LostDamagedStolenAssets"),
                        TotalAcquisitionValue = ReadDecimal(reader, "TotalAcquisitionValue"),
                        TotalCurrentBookValue = ReadDecimal(reader, "TotalCurrentBookValue"),
                        TotalAccumulatedDepreciation = ReadDecimal(reader, "TotalAccumulatedDepreciation"),
                        TotalAcquisitionAndTax = ReadDecimal(reader, "TotalAcquisitionAndTax"),
                        AverageAnnualDepreciationRatePercent = ReadDecimal(reader, "AverageAnnualDepreciationRatePercent")
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
            var rows = new List<DepartmentValueRow>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = DepartmentValuesSql;
                AddScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        rows.Add(new DepartmentValueRow
                        {
                            DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                            BookValue = ReadDecimal(reader, "BookValue"),
                            AcquisitionValue = ReadDecimal(reader, "AcquisitionValue"),
                            AssetCount = ReadInt32(reader, "AssetCount")
                        });
                    }
                }
            }

            return SelectTopDepartments(rows, TopDepartmentCount);
        }

        /// <summary>
        /// Mirrors the former SQL: GROUP BY department name (SQL default collation is
        /// case-insensitive and ignores trailing spaces), ORDER BY SUM(AcquisitionCost) DESC, name ASC, TOP n.
        /// </summary>
        internal static IList<DepartmentValueVm> SelectTopDepartments(IEnumerable<DepartmentValueRow> rows, int take)
        {
            return (rows ?? Enumerable.Empty<DepartmentValueRow>())
                .GroupBy(x => (x.DepartmentName ?? string.Empty).TrimEnd(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Name = g.First().DepartmentName,
                    AcquisitionValue = g.Sum(x => x.AcquisitionValue),
                    BookValue = g.Sum(x => x.BookValue),
                    AssetCount = g.Sum(x => x.AssetCount)
                })
                .OrderByDescending(x => x.AcquisitionValue)
                .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(take)
                .Select(x => new DepartmentValueVm
                {
                    DepartmentName = x.Name,
                    BookValue = x.BookValue,
                    AssetCount = x.AssetCount
                })
                .ToList();
        }

        private static IList<DashboardTrendPointVm> ReadAssignmentsPerMonth(
            IDbConnection connection,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            var dates = new List<DateTime>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = AssignmentDatesSql;
                AddScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
                SqlQueryHelper.AddParameter(command, "@FromDate", DateTime.UtcNow.AddMonths(-11));

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var value = reader[0];
                        if (value != null && value != DBNull.Value)
                        {
                            dates.Add(Convert.ToDateTime(value));
                        }
                    }
                }
            }

            return BucketByMonth(dates);
        }

        internal static IList<DashboardTrendPointVm> BucketByMonth(IEnumerable<DateTime> dates)
        {
            return (dates ?? Enumerable.Empty<DateTime>())
                .GroupBy(x => x.Year * 100 + x.Month)
                .OrderBy(g => g.Key)
                .Select(g => new DashboardTrendPointVm
                {
                    Label = (g.Key / 100).ToString("0000") + "-" + (g.Key % 100).ToString("00"),
                    Count = g.Count()
                })
                .ToList();
        }

        private static decimal ReadMaintenanceCost(
            IDbConnection connection,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = MaintenanceCostSql;
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

        private static int ReadInt32(IDataRecord reader, string column)
        {
            var value = reader[column];
            return value == null || value == DBNull.Value ? 0 : Convert.ToInt32(value);
        }

        private static decimal ReadDecimal(IDataRecord reader, string column)
        {
            var value = reader[column];
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        internal sealed class DepartmentValueRow
        {
            public string DepartmentName { get; set; }

            public decimal BookValue { get; set; }

            public decimal AcquisitionValue { get; set; }

            public int AssetCount { get; set; }
        }

        private sealed class StatusCounts
        {
            public int TotalAssets { get; set; }

            public int AssignedAssets { get; set; }

            public int UnassignedAssets { get; set; }

            public int MaintenanceAndDamagedAssets { get; set; }

            public int LostAssets { get; set; }

            public int LostDamagedStolenAssets { get; set; }

            public decimal TotalAcquisitionValue { get; set; }

            public decimal TotalCurrentBookValue { get; set; }

            public decimal TotalAccumulatedDepreciation { get; set; }

            public decimal TotalAcquisitionAndTax { get; set; }

            public decimal AverageAnnualDepreciationRatePercent { get; set; }
        }
    }

    /// <summary>
    /// Off-request twin of <see cref="AssetManagement.Application.Helpers.DepreciationCalculator.RefreshOrganization"/>
    /// (installed as DepreciationCalculator.BackgroundOrganizationRefresh). Loads only the columns the
    /// calculation reads, computes with the same DepreciationSettingsResolver.Resolve +
    /// DepreciationCalculator.Compute, and writes back only rows whose position changed. Each UPDATE is
    /// guarded on the values/UpdatedAt it read, so an asset edited meanwhile (which recalculates its own
    /// row) is never overwritten with a stale result.
    /// </summary>
    public static class DepreciationRefreshRunner
    {
        private const int UpdateBatchSize = 200;

        private const string AssetsSql = @"
SELECT
    a.[Id], a.[CurrentStatus], a.[AcquisitionCost], a.[SalvageValue], a.[DepreciationStartDate],
    a.[DepreciationMethod], a.[DepreciationLifeMonths], a.[UsefulLifeMonths], a.[DepreciationRatePercent],
    a.[AssetTypeId], a.[CategoryId], a.[CurrentBookValue], a.[AccumulatedDepreciation], a.[UpdatedAt]
FROM [Asset] a
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
OPTION (MAXDOP 1)";

        private const string TypesSql = @"
SELECT [Id], [DepreciationLifeMonths], [UsefulLifeMonths], [DepreciationRatePercent] FROM [AssetType]";

        private const string CategoriesSql = @"
SELECT [Id], [DefaultDepreciationLifeMonths], [DefaultUsefulLifeMonths], [DefaultDepreciationRatePercent] FROM [AssetCategory]";

        private const string UpdateSql = @"
UPDATE [Asset]
SET [CurrentBookValue] = @CurrentBookValue,
    [AccumulatedDepreciation] = @AccumulatedDepreciation,
    [UpdatedAt] = @UpdatedAt
WHERE [Id] = @Id
  AND [OrganizationId] = @OrganizationId
  AND [CurrentBookValue] = @PreviousBookValue
  AND [AccumulatedDepreciation] = @PreviousAccumulated
  AND (([UpdatedAt] IS NULL AND @PreviousUpdatedAt IS NULL) OR [UpdatedAt] = @PreviousUpdatedAt)";

        /// <summary>Returns the number of asset rows updated.</summary>
        public static int Run(ISqlConnectionFactory connectionFactory, int organizationId, DateTime asOfUtc)
        {
            var types = new Dictionary<int, AssetManagement.Domain.Entities.AssetType>();
            var categories = new Dictionary<int, AssetManagement.Domain.Entities.AssetCategory>();
            var assets = new List<AssetSnapshot>();
            var started = DateTime.UtcNow;

            using (var connection = connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = TypesSql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["Id"]);
                            types[id] = new AssetManagement.Domain.Entities.AssetType
                            {
                                Id = id,
                                DepreciationLifeMonths = ReadNullableInt(reader, "DepreciationLifeMonths"),
                                UsefulLifeMonths = ReadNullableInt(reader, "UsefulLifeMonths"),
                                DepreciationRatePercent = SqlQueryHelper.GetDecimalNullable(reader, "DepreciationRatePercent")
                            };
                        }
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = CategoriesSql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var id = Convert.ToInt32(reader["Id"]);
                            categories[id] = new AssetManagement.Domain.Entities.AssetCategory
                            {
                                Id = id,
                                DefaultDepreciationLifeMonths = ReadNullableInt(reader, "DefaultDepreciationLifeMonths"),
                                DefaultUsefulLifeMonths = ReadNullableInt(reader, "DefaultUsefulLifeMonths"),
                                DefaultDepreciationRatePercent = SqlQueryHelper.GetDecimalNullable(reader, "DefaultDepreciationRatePercent")
                            };
                        }
                    }
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = AssetsSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            assets.Add(new AssetSnapshot
                            {
                                Asset = new AssetManagement.Domain.Entities.Asset
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    CurrentStatus = (AssetStatus)Convert.ToInt32(reader["CurrentStatus"]),
                                    AcquisitionCost = SqlQueryHelper.GetDecimalNullable(reader, "AcquisitionCost"),
                                    SalvageValue = SqlQueryHelper.GetDecimal(reader, "SalvageValue"),
                                    DepreciationStartDate = Convert.ToDateTime(reader["DepreciationStartDate"]),
                                    DepreciationMethod = (DepreciationMethod)Convert.ToInt32(reader["DepreciationMethod"]),
                                    DepreciationLifeMonths = ReadNullableInt(reader, "DepreciationLifeMonths"),
                                    UsefulLifeMonths = ReadNullableInt(reader, "UsefulLifeMonths"),
                                    DepreciationRatePercent = SqlQueryHelper.GetDecimalNullable(reader, "DepreciationRatePercent"),
                                    AssetTypeId = Convert.ToInt32(reader["AssetTypeId"]),
                                    CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                    CurrentBookValue = SqlQueryHelper.GetDecimal(reader, "CurrentBookValue"),
                                    AccumulatedDepreciation = SqlQueryHelper.GetDecimal(reader, "AccumulatedDepreciation")
                                },
                                PreviousUpdatedAt = SqlQueryHelper.GetDateTimeNullable(reader, "UpdatedAt")
                            });
                        }
                    }
                }

                var changes = new List<AssetChange>();
                foreach (var snapshot in assets)
                {
                    var asset = snapshot.Asset;
                    AssetManagement.Domain.Entities.AssetType assetType;
                    types.TryGetValue(asset.AssetTypeId, out assetType);
                    AssetManagement.Domain.Entities.AssetCategory category;
                    categories.TryGetValue(asset.CategoryId, out category);
                    var settings = AssetManagement.Application.Helpers.DepreciationSettingsResolver.Resolve(asset, assetType, category);
                    var position = AssetManagement.Application.Helpers.DepreciationCalculator.Compute(asset, settings, asOfUtc);
                    if (position.CurrentBookValue == asset.CurrentBookValue
                        && position.AccumulatedDepreciation == asset.AccumulatedDepreciation)
                    {
                        continue;
                    }

                    changes.Add(new AssetChange
                    {
                        Id = asset.Id,
                        CurrentBookValue = position.CurrentBookValue,
                        AccumulatedDepreciation = position.AccumulatedDepreciation,
                        PreviousBookValue = asset.CurrentBookValue,
                        PreviousAccumulated = asset.AccumulatedDepreciation,
                        PreviousUpdatedAt = snapshot.PreviousUpdatedAt
                    });
                }

                var updated = 0;
                for (var offset = 0; offset < changes.Count; offset += UpdateBatchSize)
                {
                    using (var transaction = connection.BeginTransaction())
                    {
                        foreach (var change in changes.Skip(offset).Take(UpdateBatchSize))
                        {
                            using (var command = connection.CreateCommand())
                            {
                                command.Transaction = transaction;
                                command.CommandText = UpdateSql;
                                SqlQueryHelper.AddParameter(command, "@Id", change.Id);
                                SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                                SqlQueryHelper.AddParameter(command, "@CurrentBookValue", change.CurrentBookValue);
                                SqlQueryHelper.AddParameter(command, "@AccumulatedDepreciation", change.AccumulatedDepreciation);
                                SqlQueryHelper.AddParameter(command, "@UpdatedAt", asOfUtc);
                                SqlQueryHelper.AddParameter(command, "@PreviousBookValue", change.PreviousBookValue);
                                SqlQueryHelper.AddParameter(command, "@PreviousAccumulated", change.PreviousAccumulated);
                                SqlQueryHelper.AddParameter(command, "@PreviousUpdatedAt",
                                    change.PreviousUpdatedAt.HasValue ? (object)change.PreviousUpdatedAt.Value : DBNull.Value);
                                updated += command.ExecuteNonQuery();
                            }
                        }

                        transaction.Commit();
                    }
                }

                System.Diagnostics.Trace.TraceInformation(
                    "Depreciation background refresh org {0}: {1} assets, {2} changed, {3} updated, {4} ms",
                    organizationId,
                    assets.Count,
                    changes.Count,
                    updated,
                    (int)(DateTime.UtcNow - started).TotalMilliseconds);
                return updated;
            }
        }

        private static int? ReadNullableInt(IDataRecord reader, string column)
        {
            var value = reader[column];
            return value == null || value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        private sealed class AssetSnapshot
        {
            public AssetManagement.Domain.Entities.Asset Asset { get; set; }

            public DateTime? PreviousUpdatedAt { get; set; }
        }

        private sealed class AssetChange
        {
            public int Id { get; set; }

            public decimal CurrentBookValue { get; set; }

            public decimal AccumulatedDepreciation { get; set; }

            public decimal PreviousBookValue { get; set; }

            public decimal PreviousAccumulated { get; set; }

            public DateTime? PreviousUpdatedAt { get; set; }
        }
    }

    /// <summary>
    /// One batched, org-scoped Users lookup turning raw user ids / usernames / emails into display
    /// labels ("First Last (email)", else email, else username). Shared by Pending Approvals and the
    /// report previews (Requested By, Custodian).
    /// </summary>
    public static class UserLabelLookup
    {
        private const int KeysPerCommand = 300;

        public static IDictionary<string, string> Resolve(ISqlConnectionFactory connectionFactory, int organizationId, IEnumerable<string> keys)
        {
            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var distinct = (keys ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (distinct.Count == 0)
            {
                return labels;
            }

            using (var connection = connectionFactory.CreateConnection())
            {
                connection.Open();
                for (var offset = 0; offset < distinct.Count; offset += KeysPerCommand)
                {
                    var chunk = distinct.Skip(offset).Take(KeysPerCommand).ToList();
                    using (var command = connection.CreateCommand())
                    {
                        var parameterNames = new List<string>();
                        for (var i = 0; i < chunk.Count; i++)
                        {
                            var name = "@UserKey" + i;
                            parameterNames.Add(name);
                            SqlQueryHelper.AddParameter(command, name, chunk[i]);
                        }

                        var list = string.Join(", ", parameterNames);
                        command.CommandText = @"
SELECT u.[Id], u.[UserName], u.[Email], u.[FirstName], u.[LastName]
FROM [Users] u
WHERE u.[OrganizationId] = @OrganizationId
  AND (u.[Id] IN (" + list + @") OR u.[UserName] IN (" + list + @") OR u.[Email] IN (" + list + @"))
OPTION (MAXDOP 1)";
                        SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var label = BuildLabel(
                                    SqlQueryHelper.GetString(reader, "FirstName"),
                                    SqlQueryHelper.GetString(reader, "LastName"),
                                    SqlQueryHelper.GetString(reader, "Email"),
                                    SqlQueryHelper.GetString(reader, "UserName"));
                                if (string.IsNullOrWhiteSpace(label))
                                {
                                    continue;
                                }

                                foreach (var key in new[]
                                {
                                    SqlQueryHelper.GetString(reader, "Id"),
                                    SqlQueryHelper.GetString(reader, "UserName"),
                                    SqlQueryHelper.GetString(reader, "Email")
                                })
                                {
                                    if (!string.IsNullOrWhiteSpace(key) && !labels.ContainsKey(key.Trim()))
                                    {
                                        labels[key.Trim()] = label;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return labels;
        }

        public static string BuildLabel(string firstName, string lastName, string email, string userName)
        {
            var name = ((firstName ?? string.Empty).Trim() + " " + (lastName ?? string.Empty).Trim()).Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return string.IsNullOrWhiteSpace(email) ? name : name + " (" + email.Trim() + ")";
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                return email.Trim();
            }

            return string.IsNullOrWhiteSpace(userName) ? null : userName.Trim();
        }
    }
}
