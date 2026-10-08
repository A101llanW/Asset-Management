using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// Assets Index "Group by room" (Assets/Index?view=room). Same WHERE clause as the Assets Index
    /// list (AssetQueryService.BuildWhereClause: org, IsActive, department scope, list filters), so
    /// counts always match the flat list for the same filters.
    ///
    /// Perf (SQL Express memory-grant timeouts, see f1ed181 / 7112a1f / 66ab949): every level is
    /// aggregate-first and grant-free (0 KB requested), served by IX_Asset_Org_Active_Dept_Cat_Tag
    /// (migration 091) in key order:
    ///   * level 1 stream-aggregates by DepartmentId and is sorted/paged in memory (one row per unit);
    ///   * level 2 stream-aggregates by CategoryId inside one unit, sorted in memory;
    ///   * level 3 pages ids in AssetTag key order, then fetches the wide columns for the page by PK.
    /// No SQL ORDER BY needs a sort operator. The department-scope predicate is written as a CASE so
    /// the optimizer cannot split it into a concatenation + distinct sort (which asked for a grant).
    /// </summary>
    public class AssetRoomGroupQueryService : IAssetRoomGroupQueryService
    {
        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IDepartmentScopeService _departmentScope;

        public AssetRoomGroupQueryService(
            ISqlConnectionFactory connectionFactory,
            IOrganizationScopeService organizationScope,
            IDepartmentScopeService departmentScope)
        {
            _connectionFactory = connectionFactory;
            _organizationScope = organizationScope;
            _departmentScope = departmentScope;
        }

        public AssetRoomGroupPageVm GetRoomPage(AssetFilterVm filter, int page, int pageSize)
        {
            var scope = ResolveScope(filter);
            // FORCESEEK keeps the read on an (OrganizationId, IsActive, ...) index range in DepartmentId
            // order even with a search filter, so the GROUP BY never needs a sort (no grant).
            var sql = @"
SELECT g.[DepartmentId], g.[UnitCount], g.[PricedCount], g.[PricedTotal],
       d.[Name] AS DepartmentName, d.[DepartmentKind], p.[Name] AS ParentName
FROM (
    SELECT a.[DepartmentId],
           COUNT(*) AS UnitCount,
           COUNT(a.[AcquisitionCost]) AS PricedCount,
           SUM(a.[AcquisitionCost]) AS PricedTotal
    FROM [Asset] a WITH (FORCESEEK)" + BuildRoomWhereClause(filter) + @"
    GROUP BY a.[DepartmentId]
) g
LEFT JOIN [Department] d ON d.[Id] = g.[DepartmentId]
LEFT JOIN [Department] p ON p.[Id] = d.[ParentDepartmentId]
OPTION (LOOP JOIN, ORDER GROUP)";

            var groups = new List<AssetRoomGroupVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    scope.AddScopeParameters(command);
                    AssetQueryService.AddFilterParameters(command, filter);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            groups.Add(new AssetRoomGroupVm
                            {
                                DepartmentId = reader["DepartmentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["DepartmentId"]),
                                Name = SqlQueryHelper.GetString(reader, "DepartmentName"),
                                DepartmentKind = reader["DepartmentKind"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["DepartmentKind"]),
                                ParentName = SqlQueryHelper.GetString(reader, "ParentName"),
                                AssetCount = Convert.ToInt32(reader["UnitCount"]),
                                PricedCount = Convert.ToInt32(reader["PricedCount"]),
                                PricedTotal = ReadDecimalOrZero(reader, "PricedTotal")
                            });
                        }
                    }
                }
            }

            return AssetRoomGroupView.SortAndPage(groups, page, pageSize);
        }

        public IList<AssetRoomCategoryGroupVm> GetRoomCategories(AssetFilterVm filter, int roomId)
        {
            var scope = ResolveScope(filter);
            var sql = @"
SELECT g.[CategoryId], g.[UnitCount], g.[PricedCount], g.[PricedTotal], c.[Name] AS CategoryName
FROM (
    SELECT a.[CategoryId],
           COUNT(*) AS UnitCount,
           COUNT(a.[AcquisitionCost]) AS PricedCount,
           SUM(a.[AcquisitionCost]) AS PricedTotal
    FROM [Asset] a" + BuildRoomWhereClause(filter) + BuildRoomMatchClause(roomId) + @"
    GROUP BY a.[CategoryId]
) g
LEFT JOIN [AssetCategory] c ON c.[Id] = g.[CategoryId]
OPTION (LOOP JOIN, ORDER GROUP)";

            var items = new List<AssetRoomCategoryGroupVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    scope.AddScopeParameters(command);
                    AssetQueryService.AddFilterParameters(command, filter);
                    AddRoomParameter(command, roomId);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var name = SqlQueryHelper.GetString(reader, "CategoryName");
                            items.Add(new AssetRoomCategoryGroupVm
                            {
                                CategoryId = Convert.ToInt32(reader["CategoryId"]),
                                Name = string.IsNullOrWhiteSpace(name) ? "Uncategorized" : name,
                                AssetCount = Convert.ToInt32(reader["UnitCount"]),
                                PricedCount = Convert.ToInt32(reader["PricedCount"]),
                                PricedTotal = ReadDecimalOrZero(reader, "PricedTotal")
                            });
                        }
                    }
                }
            }

            return items
                .OrderBy(x => x.Name ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.CategoryId)
                .ToList();
        }

        public AssetRoomAssetPageVm GetRoomCategoryAssets(AssetFilterVm filter, int roomId, int categoryId, int skip, int take)
        {
            var scope = ResolveScope(filter);
            var safeTake = take <= 0 ? AssetRoomGroupView.DefaultAssetPageSize : Math.Min(take, AssetRoomGroupView.MaxAssetPageSize);
            var safeSkip = Math.Max(skip, 0);
            var whereClause = BuildRoomWhereClause(filter) + BuildRoomMatchClause(roomId) + " AND a.[CategoryId] = @RoomCategoryId";

            var countSql = "SELECT COUNT(*) FROM [Asset] a" + whereClause;
            // Page on narrow keys (index order AssetTag, Id: no sort), then fetch the page rows by PK.
            // RowNo carries the page order back so the outer SELECT needs no ORDER BY (no sort grant).
            var pageSql = @"
WITH paged AS (
    SELECT a.[Id], ROW_NUMBER() OVER (ORDER BY a.[AssetTag], a.[Id]) AS RowNo
    FROM [Asset] a" + whereClause + @"
    ORDER BY a.[AssetTag], a.[Id]
    OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY
)
SELECT p.[RowNo], a.[Id], a.[AssetTag], a.[AssetName], a.[CurrentCustodianId], a.[CurrentStatus],
       a.[AcquisitionCost], st.[Name] AS AssetSubTypeName
FROM paged p
INNER JOIN [Asset] a ON a.[Id] = p.[Id]
LEFT JOIN [AssetSubType] st ON st.[Id] = a.[AssetSubTypeId]
OPTION (LOOP JOIN)";

            var totalCount = 0;
            var rows = new List<KeyValuePair<long, AssetRoomAssetVm>>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countSql;
                    scope.AddScopeParameters(countCommand);
                    AssetQueryService.AddFilterParameters(countCommand, filter);
                    AddRoomParameter(countCommand, roomId);
                    SqlQueryHelper.AddParameter(countCommand, "@RoomCategoryId", categoryId);
                    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
                }

                if (totalCount > 0 && safeSkip < totalCount)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = pageSql;
                        scope.AddScopeParameters(command);
                        AssetQueryService.AddFilterParameters(command, filter);
                        AddRoomParameter(command, roomId);
                        SqlQueryHelper.AddParameter(command, "@RoomCategoryId", categoryId);
                        SqlQueryHelper.AddParameter(command, "@Skip", safeSkip);
                        SqlQueryHelper.AddParameter(command, "@Take", safeTake);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var item = new AssetRoomAssetVm
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                                    AssetName = SqlQueryHelper.GetString(reader, "AssetName"),
                                    AssetSubTypeName = AssetSubTypeNormalizer.NormalizeName(SqlQueryHelper.GetString(reader, "AssetSubTypeName")),
                                    CurrentCustodianId = SqlQueryHelper.GetString(reader, "CurrentCustodianId"),
                                    CurrentStatus = (AssetStatus)Convert.ToInt32(reader["CurrentStatus"]),
                                    // The asset's own price only; NULL stays null ("Not set"), never a sub-type price.
                                    Price = reader["AcquisitionCost"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(reader["AcquisitionCost"])
                                };
                                rows.Add(new KeyValuePair<long, AssetRoomAssetVm>(Convert.ToInt64(reader["RowNo"]), item));
                            }
                        }
                    }
                }
            }

            return new AssetRoomAssetPageVm
            {
                Items = rows.OrderBy(x => x.Key).Select(x => x.Value).ToList(),
                TotalCount = totalCount,
                Skip = safeSkip,
                Take = safeTake
            };
        }

        /// <summary>
        /// The Assets Index WHERE clause with the department-scope OR rewritten as an equivalent CASE
        /// (same parameters, same truth table). Falls back to the unchanged clause if the shared scope
        /// text ever changes, which is still correct, just not guaranteed grant-free.
        /// </summary>
        internal static string BuildRoomWhereClause(AssetFilterVm filter)
        {
            var whereClause = AssetQueryService.BuildWhereClause(filter);
            var orScope = SqlQueryHelper.FormatAssetDepartmentScopeSql("a");
            return whereClause.Replace(orScope, CaseDepartmentScopeSql);
        }

        internal const string CaseDepartmentScopeSql =
            "(CASE WHEN @BypassDepartmentScope = 1 THEN 1"
            + " WHEN @DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND a.[DepartmentId] = @DepartmentId THEN 1"
            + " WHEN @IncludeClassDepartmentAssets = 1 AND EXISTS (SELECT 1 FROM [Department] dc WHERE dc.[Id] = a.[DepartmentId] AND dc.[DepartmentKind] = 2 AND dc.[IsActive] = 1) THEN 1"
            + " ELSE 0 END) = 1";

        private static string BuildRoomMatchClause(int roomId)
        {
            return roomId == AssetRoomGroupView.UnassignedRoomId
                ? " AND a.[DepartmentId] IS NULL"
                : " AND a.[DepartmentId] = @RoomDepartmentId";
        }

        private static void AddRoomParameter(IDbCommand command, int roomId)
        {
            if (roomId != AssetRoomGroupView.UnassignedRoomId)
            {
                SqlQueryHelper.AddParameter(command, "@RoomDepartmentId", roomId);
            }
        }

        private static decimal ReadDecimalOrZero(IDataRecord record, string column)
        {
            var value = record[column];
            return value == null || value == DBNull.Value ? 0m : Convert.ToDecimal(value);
        }

        private TenantQueryScope ResolveScope(AssetFilterVm filter)
        {
            if (filter != null && filter.OrganizationWide)
            {
                return TenantQueryScope.ForOrganizationOnly(_organizationScope);
            }

            return TenantQueryScope.Resolve(_organizationScope, _departmentScope);
        }
    }
}
