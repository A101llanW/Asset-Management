using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class AssetQueryService : IAssetQueryService
    {
        private const int DefaultExportMaxRows = 50000;

        private const string SelectColumns = @"
SELECT
    a.[Id],
    a.[AssetTag],
    a.[AssetName],
    a.[CategoryId],
    a.[SerialNumber],
    a.[CurrentCustodianId],
    a.[CurrentStatus],
    a.[AcquisitionCost],
    a.[DepartmentId],
    a.[AssetTypeId],
    a.[AssetSubTypeId],
    a.[Brand],
    a.[Model],
    st.[Name] AS AssetSubTypeName,
    at.[Name] AS AssetTypeName,
    c.[Name] AS CategoryName,
    d.[Name] AS DepartmentName";

        private const string ExportSelectColumns = @"
SELECT
    a.[AssetTag],
    a.[AssetName],
    a.[CurrentStatus],
    c.[Name] AS CategoryName,
    d.[Name] AS DepartmentName,
    a.[CurrentCustodianId],
    a.[AcquisitionCost],
    a.[PurchaseDate],
    a.[SerialNumber]";

        private const string FromClause = @"
FROM [Asset] a
LEFT JOIN [AssetCategory] c ON c.[Id] = a.[CategoryId]
LEFT JOIN [AssetType] at ON at.[Id] = a.[AssetTypeId]
LEFT JOIN [Department] d ON d.[Id] = a.[DepartmentId]
LEFT JOIN [AssetSubType] st ON st.[Id] = a.[AssetSubTypeId]";

        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IDepartmentScopeService _departmentScope;

        public AssetQueryService(
            ISqlConnectionFactory connectionFactory,
            IOrganizationScopeService organizationScope,
            IDepartmentScopeService departmentScope)
        {
            _connectionFactory = connectionFactory;
            _organizationScope = organizationScope;
            _departmentScope = departmentScope;
        }

        public AssetListPageVm GetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            var scope = ResolveScope(filter);
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var totalCount = CountInternal(scope, filter);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);
            var skip = (safePage - 1) * safePageSize;

            var items = new List<AssetListVm>();
            var whereClause = BuildWhereClause(filter);
            var orderBy = BuildOrderBy(sort, direction);
            var sql = SelectColumns + FromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    scope.AddScopeParameters(command);
                    AddFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", safePageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapAssetListItem(reader));
                        }
                    }
                }
            }

            return new AssetListPageVm
            {
                Items = items,
                TotalCount = totalCount,
                Search = filter == null ? null : filter.Search,
                Sort = sort,
                Direction = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc",
                Page = safePage,
                PageSize = safePageSize
            };
        }

        public AssetGroupListPageVm GetGroupedListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            var groupBy = AssetListGroupBy.Normalize(filter == null ? null : filter.GroupBy);
            if (AssetListGroupBy.IsProduct(groupBy))
            {
                return GetProductGroupedListPage(filter, sort, direction, page, pageSize);
            }

            return GetSimpleDimensionGroupedListPage(filter, sort, direction, page, pageSize, groupBy);
        }

        private AssetGroupListPageVm GetProductGroupedListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            var scope = ResolveScope(filter);
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var whereClause = BuildWhereClause(filter);
            var orderBy = BuildGroupedOrderBy(sort, direction);
            var countSql = @"
SELECT COUNT(*) FROM (
    SELECT 1 AS grp
    " + FromClause + whereClause + @"
    GROUP BY a.[AssetName], a.[AssetSubTypeId], a.[DepartmentId], a.[CurrentStatus], a.[AssetTypeId]
) grouped";

            var totalCount = 0;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countSql;
                    scope.AddScopeParameters(countCommand);
                    AddFilterParameters(countCommand, filter);
                    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
                }

                var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
                var safePage = Math.Min(Math.Max(page, 1), totalPages);
                var skip = (safePage - 1) * safePageSize;
                var sql = @"
SELECT
    MIN(a.[Id]) AS RepresentativeId,
    a.[AssetName],
    a.[AssetSubTypeId],
    a.[DepartmentId],
    a.[CurrentStatus],
    a.[AssetTypeId],
    COUNT(*) AS UnitCount,
    SUM(a.[AcquisitionCost]) AS TotalAcquisitionCost,
    MAX(c.[Name]) AS CategoryName,
    MAX(d.[Name]) AS DepartmentName,
    MAX(st.[Name]) AS AssetSubTypeName
" + FromClause + whereClause + @"
GROUP BY a.[AssetName], a.[AssetSubTypeId], a.[DepartmentId], a.[CurrentStatus], a.[AssetTypeId]
ORDER BY " + orderBy + @"
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

                var items = new List<AssetGroupListVm>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    scope.AddScopeParameters(command);
                    AddFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", safePageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var subTypeId = reader["AssetSubTypeId"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["AssetSubTypeId"]);
                            var departmentId = reader["DepartmentId"] == DBNull.Value
                                ? (int?)null
                                : Convert.ToInt32(reader["DepartmentId"]);
                            var status = (AssetStatus)Convert.ToInt32(reader["CurrentStatus"]);
                            var assetName = SqlQueryHelper.GetString(reader, "AssetName");
                            items.Add(new AssetGroupListVm
                            {
                                GroupKey = BuildGroupKey(assetName, subTypeId, departmentId, status),
                                AssetName = assetName,
                                AssetSubTypeId = subTypeId,
                                DepartmentId = departmentId,
                                CurrentStatus = status,
                                Count = Convert.ToInt32(reader["UnitCount"]),
                                TotalAcquisitionCost = Convert.ToDecimal(reader["TotalAcquisitionCost"]),
                                CategoryName = SqlQueryHelper.GetString(reader, "CategoryName"),
                                DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                                AssetSubTypeName = AssetSubTypeNormalizer.NormalizeName(SqlQueryHelper.GetString(reader, "AssetSubTypeName"))
                            });
                        }
                    }
                }

                return new AssetGroupListPageVm
                {
                    Items = items,
                    TotalCount = totalCount,
                    Search = filter == null ? null : filter.Search,
                    Sort = sort,
                    Direction = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc",
                    Page = safePage,
                    PageSize = safePageSize,
                    GroupBy = AssetListGroupBy.Product
                };
            }
        }

        private AssetGroupListPageVm GetSimpleDimensionGroupedListPage(
            AssetFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            string groupBy)
        {
            var dimension = ResolveListGroupDimension(groupBy);
            var scope = ResolveScope(filter);
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var whereClause = BuildWhereClause(filter);
            var orderBy = BuildSimpleDimensionGroupedOrderBy(sort, direction, dimension);
            var countSql = @"
SELECT COUNT(*) FROM (
    SELECT 1 AS grp
    " + FromClause + whereClause + @"
    GROUP BY " + dimension.GroupBySql + @"
) grouped";

            var totalCount = 0;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countSql;
                    scope.AddScopeParameters(countCommand);
                    AddFilterParameters(countCommand, filter);
                    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
                }

                var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
                var safePage = Math.Min(Math.Max(page, 1), totalPages);
                var skip = (safePage - 1) * safePageSize;
                var sql = @"
SELECT
    " + dimension.GroupBySql + @" AS GroupDimensionValue,
    COUNT(*) AS UnitCount,
    SUM(a.[AcquisitionCost]) AS TotalAcquisitionCost,
    " + dimension.LabelSelectSql + @"
" + FromClause + whereClause + @"
GROUP BY " + dimension.GroupBySql + @"
ORDER BY " + orderBy + @"
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

                var items = new List<AssetGroupListVm>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    scope.AddScopeParameters(command);
                    AddFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", safePageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapSimpleDimensionGroupItem(reader, dimension, groupBy));
                        }
                    }
                }

                return new AssetGroupListPageVm
                {
                    Items = items,
                    TotalCount = totalCount,
                    Search = filter == null ? null : filter.Search,
                    Sort = sort,
                    Direction = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc",
                    Page = safePage,
                    PageSize = safePageSize,
                    GroupBy = groupBy
                };
            }
        }

        public AssetGroupMembersPageVm GetGroupMembers(
            AssetFilterVm filter,
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus? groupStatus,
            int? groupCategoryId,
            int? groupAssetTypeId,
            int skip,
            int take)
        {
            var groupBy = AssetListGroupBy.Normalize(filter == null ? null : filter.GroupBy);
            if (!AssetListGroupBy.IsProduct(groupBy))
            {
                return GetSimpleDimensionGroupMembers(
                    filter,
                    groupBy,
                    groupCategoryId,
                    groupAssetTypeId,
                    assetSubTypeId,
                    groupDepartmentId,
                    groupStatus,
                    skip,
                    take);
            }

            var scope = ResolveScope(filter);
            var safeTake = take <= 0 ? 10 : Math.Min(take, 100);
            var safeSkip = Math.Max(skip, 0);
            var groupMemberClause = BuildGroupMemberWhereClause();

            var whereClause = BuildWhereClause(filter) + groupMemberClause;
            var countSql = "SELECT COUNT(*)" + FromClause + whereClause;

            var totalCount = 0;
            var members = new List<AssetListVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countSql;
                    scope.AddScopeParameters(countCommand);
                    AddFilterParameters(countCommand, filter);
                    AddGroupMemberParameters(countCommand, assetName, assetSubTypeId, groupDepartmentId, groupStatus);
                    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
                }

                if (totalCount > 0 && safeSkip < totalCount)
                {
                    var sql = SelectColumns + FromClause + whereClause
                        + " ORDER BY a.[AssetTag] ASC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        scope.AddScopeParameters(command);
                        AddFilterParameters(command, filter);
                        AddGroupMemberParameters(command, assetName, assetSubTypeId, groupDepartmentId, groupStatus);
                        SqlQueryHelper.AddParameter(command, "@Skip", safeSkip);
                        SqlQueryHelper.AddParameter(command, "@Take", safeTake);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                members.Add(MapAssetListItem(reader));
                            }
                        }
                    }
                }
            }

            return new AssetGroupMembersPageVm
            {
                Items = members,
                TotalCount = totalCount,
                Skip = safeSkip,
                Take = safeTake
            };
        }

        private AssetGroupMembersPageVm GetSimpleDimensionGroupMembers(
            AssetFilterVm filter,
            string groupBy,
            int? groupCategoryId,
            int? groupAssetTypeId,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus? groupStatus,
            int skip,
            int take)
        {
            var scope = ResolveScope(filter);
            var safeTake = take <= 0 ? 10 : Math.Min(take, 100);
            var safeSkip = Math.Max(skip, 0);
            var groupMemberClause = BuildSimpleDimensionMemberWhereClause(groupBy);
            var whereClause = BuildWhereClause(filter) + groupMemberClause;
            var countSql = "SELECT COUNT(*)" + FromClause + whereClause;

            var totalCount = 0;
            var members = new List<AssetListVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var countCommand = connection.CreateCommand())
                {
                    countCommand.CommandText = countSql;
                    scope.AddScopeParameters(countCommand);
                    AddFilterParameters(countCommand, filter);
                    AddSimpleDimensionMemberParameters(
                        countCommand,
                        groupBy,
                        groupCategoryId,
                        groupAssetTypeId,
                        assetSubTypeId,
                        groupDepartmentId,
                        groupStatus);
                    totalCount = Convert.ToInt32(countCommand.ExecuteScalar());
                }

                if (totalCount > 0 && safeSkip < totalCount)
                {
                    var sql = SelectColumns + FromClause + whereClause
                        + " ORDER BY a.[AssetTag] ASC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sql;
                        scope.AddScopeParameters(command);
                        AddFilterParameters(command, filter);
                        AddSimpleDimensionMemberParameters(
                            command,
                            groupBy,
                            groupCategoryId,
                            groupAssetTypeId,
                            assetSubTypeId,
                            groupDepartmentId,
                            groupStatus);
                        SqlQueryHelper.AddParameter(command, "@Skip", safeSkip);
                        SqlQueryHelper.AddParameter(command, "@Take", safeTake);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                members.Add(MapAssetListItem(reader));
                            }
                        }
                    }
                }
            }

            return new AssetGroupMembersPageVm
            {
                Items = members,
                TotalCount = totalCount,
                Skip = safeSkip,
                Take = safeTake
            };
        }

        private static string BuildGroupMemberWhereClause()
        {
            return @" AND a.[AssetName] = @GroupAssetName
 AND ((@GroupAssetSubTypeId IS NULL AND a.[AssetSubTypeId] IS NULL) OR a.[AssetSubTypeId] = @GroupAssetSubTypeId)
 AND ((@GroupDepartmentId IS NULL AND a.[DepartmentId] IS NULL) OR a.[DepartmentId] = @GroupDepartmentId)
 AND (@GroupCurrentStatus IS NULL OR a.[CurrentStatus] = @GroupCurrentStatus)";
        }

        private static void AddGroupMemberParameters(
            IDbCommand command,
            string assetName,
            int? assetSubTypeId,
            int? departmentId,
            AssetStatus? status)
        {
            SqlQueryHelper.AddParameter(command, "@GroupAssetName", assetName);
            SqlQueryHelper.AddParameter(command, "@GroupAssetSubTypeId",
                assetSubTypeId.HasValue ? (object)assetSubTypeId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@GroupDepartmentId",
                departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@GroupCurrentStatus",
                status.HasValue ? (object)(int)status.Value : DBNull.Value);
        }

        private static string BuildGroupKey(string assetName, int? assetSubTypeId, int? departmentId, AssetStatus status)
        {
            return (assetName ?? string.Empty) + "|"
                + (assetSubTypeId.HasValue ? assetSubTypeId.Value.ToString() : "0") + "|"
                + (departmentId.HasValue ? departmentId.Value.ToString() : "0") + "|"
                + ((int)status).ToString();
        }

        private static string BuildSimpleDimensionGroupKey(string prefix, string valueToken)
        {
            return prefix + "|" + (valueToken ?? "0");
        }

        private static string BuildSimpleDimensionMemberWhereClause(string groupBy)
        {
            switch (AssetListGroupBy.Normalize(groupBy))
            {
                case AssetListGroupBy.Category:
                    return " AND a.[CategoryId] = @GroupCategoryId";
                case AssetListGroupBy.Type:
                    return " AND a.[AssetTypeId] = @GroupAssetTypeId";
                case AssetListGroupBy.SubType:
                    return @" AND ((@GroupAssetSubTypeId IS NULL AND a.[AssetSubTypeId] IS NULL) OR a.[AssetSubTypeId] = @GroupAssetSubTypeId)";
                case AssetListGroupBy.Status:
                    return " AND a.[CurrentStatus] = @GroupCurrentStatus";
                default:
                    return @" AND ((@GroupDepartmentId IS NULL AND a.[DepartmentId] IS NULL) OR a.[DepartmentId] = @GroupDepartmentId)";
            }
        }

        private static void AddSimpleDimensionMemberParameters(
            IDbCommand command,
            string groupBy,
            int? groupCategoryId,
            int? groupAssetTypeId,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus? groupStatus)
        {
            switch (AssetListGroupBy.Normalize(groupBy))
            {
                case AssetListGroupBy.Category:
                    if (!groupCategoryId.HasValue)
                    {
                        throw new InvalidOperationException("Category group members require groupCategoryId.");
                    }

                    SqlQueryHelper.AddParameter(command, "@GroupCategoryId", groupCategoryId.Value);
                    return;
                case AssetListGroupBy.Type:
                    if (!groupAssetTypeId.HasValue)
                    {
                        throw new InvalidOperationException("Type group members require groupAssetTypeId.");
                    }

                    SqlQueryHelper.AddParameter(command, "@GroupAssetTypeId", groupAssetTypeId.Value);
                    return;
                case AssetListGroupBy.SubType:
                    SqlQueryHelper.AddParameter(command, "@GroupAssetSubTypeId",
                        assetSubTypeId.HasValue ? (object)assetSubTypeId.Value : DBNull.Value);
                    return;
                case AssetListGroupBy.Status:
                    if (!groupStatus.HasValue)
                    {
                        throw new InvalidOperationException("Status group members require groupStatus.");
                    }

                    SqlQueryHelper.AddParameter(command, "@GroupCurrentStatus", (int)groupStatus.Value);
                    return;
                default:
                    SqlQueryHelper.AddParameter(command, "@GroupDepartmentId",
                        groupDepartmentId.HasValue ? (object)groupDepartmentId.Value : DBNull.Value);
                    return;
            }
        }

        private static ListGroupDimensionDefinition ResolveListGroupDimension(string groupBy)
        {
            switch (AssetListGroupBy.Normalize(groupBy))
            {
                case AssetListGroupBy.Category:
                    return new ListGroupDimensionDefinition
                    {
                        GroupBySql = "a.[CategoryId]",
                        LabelSelectSql = "MAX(c.[Name]) AS GroupLabel",
                        OrderLabelSql = "MAX(c.[Name])",
                        KeyPrefix = "cat"
                    };
                case AssetListGroupBy.Type:
                    return new ListGroupDimensionDefinition
                    {
                        GroupBySql = "a.[AssetTypeId]",
                        LabelSelectSql = "MAX(at.[Name]) AS GroupLabel",
                        OrderLabelSql = "MAX(at.[Name])",
                        KeyPrefix = "type"
                    };
                case AssetListGroupBy.SubType:
                    return new ListGroupDimensionDefinition
                    {
                        GroupBySql = "a.[AssetSubTypeId]",
                        LabelSelectSql = "MAX(st.[Name]) AS GroupLabel",
                        OrderLabelSql = "MAX(st.[Name])",
                        KeyPrefix = "subtype"
                    };
                case AssetListGroupBy.Status:
                    return new ListGroupDimensionDefinition
                    {
                        GroupBySql = "a.[CurrentStatus]",
                        LabelSelectSql = "a.[CurrentStatus] AS GroupStatus",
                        OrderLabelSql = "a.[CurrentStatus]",
                        KeyPrefix = "status",
                        IsStatus = true
                    };
                default:
                    return new ListGroupDimensionDefinition
                    {
                        GroupBySql = "a.[DepartmentId]",
                        LabelSelectSql = "MAX(d.[Name]) AS GroupLabel",
                        OrderLabelSql = "MAX(d.[Name])",
                        KeyPrefix = "dept",
                        AllowsNullLabel = true,
                        NullLabel = "Company custody"
                    };
            }
        }

        private static AssetGroupListVm MapSimpleDimensionGroupItem(
            IDataReader reader,
            ListGroupDimensionDefinition dimension,
            string groupBy)
        {
            var item = new AssetGroupListVm
            {
                Count = Convert.ToInt32(reader["UnitCount"]),
                TotalAcquisitionCost = Convert.ToDecimal(reader["TotalAcquisitionCost"])
            };

            if (dimension.IsStatus)
            {
                var status = (AssetStatus)Convert.ToInt32(reader["GroupDimensionValue"]);
                item.CurrentStatus = status;
                item.GroupLabel = status.ToString();
                item.GroupKey = BuildSimpleDimensionGroupKey(dimension.KeyPrefix, ((int)status).ToString());
                return item;
            }

            int? nullableId = reader["GroupDimensionValue"] == DBNull.Value
                ? (int?)null
                : Convert.ToInt32(reader["GroupDimensionValue"]);
            var label = SqlQueryHelper.GetString(reader, "GroupLabel");
            if (dimension.AllowsNullLabel && !nullableId.HasValue)
            {
                label = dimension.NullLabel;
            }
            else if (string.Equals(groupBy, AssetListGroupBy.SubType, StringComparison.OrdinalIgnoreCase)
                && !nullableId.HasValue)
            {
                label = "No sub-type";
            }
            else if (string.Equals(groupBy, AssetListGroupBy.Category, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(label))
            {
                label = "Uncategorized";
            }
            else if (string.IsNullOrWhiteSpace(label))
            {
                label = "Unspecified";
            }
            else if (string.Equals(groupBy, AssetListGroupBy.SubType, StringComparison.OrdinalIgnoreCase))
            {
                label = AssetSubTypeNormalizer.NormalizeName(label);
            }

            item.GroupLabel = label;
            item.AssetName = label;
            item.GroupKey = BuildSimpleDimensionGroupKey(dimension.KeyPrefix, nullableId.HasValue ? nullableId.Value.ToString() : "0");

            if (string.Equals(groupBy, AssetListGroupBy.Category, StringComparison.OrdinalIgnoreCase))
            {
                item.CategoryId = nullableId;
                item.CategoryName = label;
            }
            else if (string.Equals(groupBy, AssetListGroupBy.Type, StringComparison.OrdinalIgnoreCase))
            {
                item.AssetTypeId = nullableId;
            }
            else if (string.Equals(groupBy, AssetListGroupBy.SubType, StringComparison.OrdinalIgnoreCase))
            {
                item.AssetSubTypeId = nullableId;
                item.AssetSubTypeName = label;
            }
            else if (string.Equals(groupBy, AssetListGroupBy.Department, StringComparison.OrdinalIgnoreCase))
            {
                item.DepartmentId = nullableId;
                item.DepartmentName = label;
            }

            return item;
        }

        private sealed class ListGroupDimensionDefinition
        {
            public string GroupBySql { get; set; }

            public string LabelSelectSql { get; set; }

            public string OrderLabelSql { get; set; }

            public string KeyPrefix { get; set; }

            public bool IsStatus { get; set; }

            public bool AllowsNullLabel { get; set; }

            public string NullLabel { get; set; }
        }

        private static string BuildGroupedOrderBy(string sort, string direction)
        {
            var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sort ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "name":
                    return desc ? "a.[AssetName] DESC" : "a.[AssetName] ASC";
                case "department":
                    return desc ? "MAX(d.[Name]) DESC" : "MAX(d.[Name]) ASC";
                case "status":
                    return desc ? "a.[CurrentStatus] DESC" : "a.[CurrentStatus] ASC";
                case "acquisition":
                    return desc ? "SUM(a.[AcquisitionCost]) DESC" : "SUM(a.[AcquisitionCost]) ASC";
                default:
                    return desc ? "COUNT(*) DESC, a.[AssetName] DESC" : "COUNT(*) ASC, a.[AssetName] ASC";
            }
        }

        private static string BuildSimpleDimensionGroupedOrderBy(string sort, string direction, ListGroupDimensionDefinition dimension)
        {
            var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sort ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "department":
                case "category":
                case "name":
                    return desc ? dimension.OrderLabelSql + " DESC" : dimension.OrderLabelSql + " ASC";
                case "status":
                    return desc ? "a.[CurrentStatus] DESC" : "a.[CurrentStatus] ASC";
                case "acquisition":
                    return desc ? "SUM(a.[AcquisitionCost]) DESC" : "SUM(a.[AcquisitionCost]) ASC";
                default:
                    return desc
                        ? "COUNT(*) DESC, " + dimension.OrderLabelSql + " DESC"
                        : "COUNT(*) ASC, " + dimension.OrderLabelSql + " ASC";
            }
        }

        public int Count(AssetFilterVm filter)
        {
            var scope = TenantQueryScope.Resolve(_organizationScope, _departmentScope);
            return CountInternal(scope, filter);
        }

        public AssetExportResultVm StreamExport(AssetFilterVm filter, string sort, string direction, Action<AssetExportRowVm> writeRow, int? maxRows = null)
        {
            if (writeRow == null)
            {
                throw new ArgumentNullException("writeRow");
            }

            var scope = TenantQueryScope.Resolve(_organizationScope, _departmentScope);
            var exportMax = maxRows ?? ResolveExportMaxRows();
            var whereClause = BuildWhereClause(filter);
            var orderBy = BuildOrderBy(sort, direction);
            var sql = ExportSelectColumns + FromClause + whereClause + " ORDER BY " + orderBy;

            var rowCount = 0;
            var truncated = false;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    scope.AddScopeParameters(command);
                    AddFilterParameters(command, filter);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (rowCount >= exportMax)
                            {
                                truncated = true;
                                break;
                            }

                            writeRow(MapExportRow(reader));
                            rowCount++;
                        }
                    }
                }
            }

            return new AssetExportResultVm
            {
                RowCount = rowCount,
                Truncated = truncated,
                WarningMessage = truncated
                    ? "Export limited to " + exportMax + " rows. Refine filters to export additional records."
                    : null
            };
        }

        private static int ResolveExportMaxRows()
        {
            var configured = ConfigurationManager.AppSettings["assetExportMaxRows"];
            int parsed;
            if (!string.IsNullOrWhiteSpace(configured) && int.TryParse(configured, out parsed) && parsed > 0)
            {
                return parsed;
            }

            return DefaultExportMaxRows;
        }

        private static AssetExportRowVm MapExportRow(IDataRecord record)
        {
            return new AssetExportRowVm
            {
                AssetTag = SqlQueryHelper.GetString(record, "AssetTag"),
                AssetName = SqlQueryHelper.GetString(record, "AssetName"),
                CurrentStatus = (AssetStatus)SqlQueryHelper.GetInt32(record, "CurrentStatus"),
                CategoryName = SqlQueryHelper.GetString(record, "CategoryName"),
                DepartmentName = SqlQueryHelper.GetString(record, "DepartmentName"),
                CurrentCustodianId = SqlQueryHelper.GetString(record, "CurrentCustodianId"),
                AcquisitionCost = SqlQueryHelper.GetDecimal(record, "AcquisitionCost"),
                PurchaseDate = SqlQueryHelper.GetDateTimeNullable(record, "PurchaseDate"),
                SerialNumber = SqlQueryHelper.GetString(record, "SerialNumber")
            };
        }

        private int CountInternal(TenantQueryScope scope, AssetFilterVm filter)
        {
            var whereClause = BuildWhereClause(filter);
            var sql = "SELECT COUNT(*)" + FromClause + whereClause;

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    scope.AddScopeParameters(command);
                    AddFilterParameters(command, filter);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private static string BuildWhereClause(AssetFilterVm filter)
        {
            var clauses = new List<string>
            {
                "a.[OrganizationId] = @OrganizationId",
                "a.[IsActive] = 1",
                SqlQueryHelper.FormatAssetDepartmentScopeSql("a")
            };

            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    clauses.Add(@"(
    a.[AssetTag] LIKE @SearchPrefix
    OR a.[AssetName] LIKE @SearchPrefix
    OR a.[SerialNumber] LIKE @SearchPrefix
    OR a.[BarcodeOrQRCode] LIKE @SearchPrefix
)");
                }

                if (filter.DepartmentId.HasValue)
                {
                    clauses.Add("a.[DepartmentId] = @FilterDepartmentId");
                }

                if (filter.CategoryId.HasValue)
                {
                    clauses.Add("a.[CategoryId] = @CategoryId");
                }

                if (filter.AssetTypeId.HasValue)
                {
                    clauses.Add("a.[AssetTypeId] = @AssetTypeId");
                }

                if (filter.AssetSubTypeId.HasValue)
                {
                    clauses.Add("a.[AssetSubTypeId] = @AssetSubTypeId");
                }

                if (filter.SupplierId.HasValue)
                {
                    clauses.Add("a.[SupplierId] = @SupplierId");
                }

                if (filter.Status.HasValue)
                {
                    clauses.Add("a.[CurrentStatus] = @Status");
                }

                if (!string.IsNullOrWhiteSpace(filter.CustodianUserId))
                {
                    clauses.Add("a.[CurrentCustodianId] = @CustodianUserId");
                }

                if (filter.UnassignedOnly)
                {
                    clauses.Add("(a.[CurrentCustodianId] IS NULL OR LTRIM(RTRIM(a.[CurrentCustodianId])) = '')");
                }
            }

            return " WHERE " + string.Join(" AND ", clauses.ToArray());
        }

        private static void AddFilterParameters(IDbCommand command, AssetFilterVm filter)
        {
            var searchPrefix = filter == null || string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : SqlQueryHelper.BuildPrefixPattern(filter.Search);
            SqlQueryHelper.AddParameter(command, "@SearchPrefix", searchPrefix ?? (object)DBNull.Value);

            if (filter == null)
            {
                return;
            }

            SqlQueryHelper.AddParameter(command, "@FilterDepartmentId",
                filter.DepartmentId.HasValue ? (object)filter.DepartmentId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@CategoryId",
                filter.CategoryId.HasValue ? (object)filter.CategoryId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@AssetTypeId",
                filter.AssetTypeId.HasValue ? (object)filter.AssetTypeId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@AssetSubTypeId",
                filter.AssetSubTypeId.HasValue ? (object)filter.AssetSubTypeId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@SupplierId",
                filter.SupplierId.HasValue ? (object)filter.SupplierId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@Status",
                filter.Status.HasValue ? (object)(int)filter.Status.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@CustodianUserId",
                string.IsNullOrWhiteSpace(filter.CustodianUserId) ? (object)DBNull.Value : filter.CustodianUserId.Trim());
        }

        private static string BuildOrderBy(string sort, string direction)
        {
            var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sort ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "name":
                    return desc ? "a.[AssetName] DESC, a.[Id] DESC" : "a.[AssetName] ASC, a.[Id] ASC";
                case "category":
                    return desc ? "c.[Name] DESC, a.[Id] DESC" : "c.[Name] ASC, a.[Id] ASC";
                case "department":
                    return desc ? "d.[Name] DESC, a.[Id] DESC" : "d.[Name] ASC, a.[Id] ASC";
                case "status":
                    return desc ? "a.[CurrentStatus] DESC, a.[Id] DESC" : "a.[CurrentStatus] ASC, a.[Id] ASC";
                case "bookvalue":
                case "acquisition":
                    return desc ? "a.[AcquisitionCost] DESC, a.[Id] DESC" : "a.[AcquisitionCost] ASC, a.[Id] ASC";
                case "created":
                    return desc ? "a.[CreatedAt] DESC, a.[Id] DESC" : "a.[CreatedAt] ASC, a.[Id] ASC";
                default:
                    return desc ? "a.[AssetTag] DESC, a.[Id] DESC" : "a.[AssetTag] ASC, a.[Id] ASC";
            }
        }

        private TenantQueryScope ResolveScope(AssetFilterVm filter)
        {
            if (filter != null && filter.OrganizationWide)
            {
                return TenantQueryScope.ForOrganizationOnly(_organizationScope);
            }

            return TenantQueryScope.Resolve(_organizationScope, _departmentScope);
        }

        private static AssetListVm MapAssetListItem(IDataRecord record)
        {
            return new AssetListVm
            {
                Id = Convert.ToInt32(record["Id"]),
                AssetTag = SqlQueryHelper.GetString(record, "AssetTag"),
                AssetName = SqlQueryHelper.GetString(record, "AssetName"),
                CategoryId = Convert.ToInt32(record["CategoryId"]),
                CategoryName = SqlQueryHelper.GetString(record, "CategoryName"),
                SerialNumber = SqlQueryHelper.GetString(record, "SerialNumber"),
                DepartmentName = SqlQueryHelper.GetString(record, "DepartmentName"),
                DepartmentId = record["DepartmentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(record["DepartmentId"]),
                AssetTypeId = Convert.ToInt32(record["AssetTypeId"]),
                AssetTypeName = SqlQueryHelper.GetString(record, "AssetTypeName"),
                AssetSubTypeId = record["AssetSubTypeId"] == DBNull.Value ? (int?)null : Convert.ToInt32(record["AssetSubTypeId"]),
                Brand = SqlQueryHelper.GetString(record, "Brand"),
                Model = SqlQueryHelper.GetString(record, "Model"),
                AssetSubTypeName = AssetSubTypeNormalizer.NormalizeName(SqlQueryHelper.GetString(record, "AssetSubTypeName")),
                CurrentCustodianId = SqlQueryHelper.GetString(record, "CurrentCustodianId"),
                CurrentStatus = (AssetStatus)Convert.ToInt32(record["CurrentStatus"]),
                AcquisitionCost = Convert.ToDecimal(record["AcquisitionCost"])
            };
        }
    }
}
