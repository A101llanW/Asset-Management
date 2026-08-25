using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class CatalogQueryRepository : ICatalogQueryRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public CatalogQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public PagedListVm<RoleVm> GetRoleListPage(
            int organizationId,
            string search,
            bool? isActive,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildRoleWhereClause(search, isActive);
            var orderBy = BuildRoleOrderBy(sort, direction);
            var totalCount = Count(
                "SELECT COUNT(*) FROM [Roles] r WHERE (r.[OrganizationId] = @OrganizationId OR r.[OrganizationId] IS NULL)" + whereClause,
                command => AddRoleFilterParameters(command, organizationId, search, isActive));
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = new List<RoleVm>();
            var sql = @"
SELECT r.[Id], r.[Name], r.[Description], r.[IsSystemRole], r.[IsActive]
FROM [Roles] r
WHERE (r.[OrganizationId] = @OrganizationId OR r.[OrganizationId] IS NULL)"
                + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            ExecuteReader(sql, command =>
            {
                AddRoleFilterParameters(command, organizationId, search, isActive);
                SqlQueryHelper.AddParameter(command, "@Skip", skip);
                SqlQueryHelper.AddParameter(command, "@Take", safePageSize);
            }, reader =>
            {
                while (reader.Read())
                {
                    items.Add(new RoleVm
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = SqlQueryHelper.GetString(reader, "Name"),
                        Description = SqlQueryHelper.GetString(reader, "Description"),
                        IsSystemRole = Convert.ToBoolean(reader["IsSystemRole"]),
                        IsActive = Convert.ToBoolean(reader["IsActive"])
                    });
                }
            });

            return BuildResult(items, totalCount, search, sort, direction, safePage, safePageSize);
        }

        public PagedListVm<SupplierVm> GetSupplierListPage(
            int organizationId,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildSupplierWhereClause(search);
            var orderBy = BuildSupplierOrderBy(sort, direction);
            var fromClause = @"
FROM [Supplier] s
LEFT JOIN (
    SELECT [SupplierId], COUNT(*) AS CatalogItemCount, MIN([UnitPrice]) AS CatalogMinPrice, MAX([UnitPrice]) AS CatalogMaxPrice
    FROM [SupplierCatalogItem]
    WHERE [IsActive] = 1
    GROUP BY [SupplierId]
) stats ON stats.[SupplierId] = s.[Id]
WHERE s.[OrganizationId] = @OrganizationId";

            var totalCount = Count(
                "SELECT COUNT(*) " + fromClause + whereClause,
                command => AddSupplierFilterParameters(command, organizationId, search));
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = new List<SupplierVm>();
            var sql = @"
SELECT
    s.[Id],
    s.[SupplierName],
    s.[ContactPerson],
    s.[Email],
    s.[Phone],
    s.[Address],
    s.[RegistrationNumber],
    s.[TaxId],
    s.[PaymentTerms],
    s.[DefaultLeadTimeDays],
    s.[Website],
    s.[IsPreferred],
    s.[Country],
    s.[PaymentInstructions],
    s.[Notes],
    s.[IsActive],
    ISNULL(stats.[CatalogItemCount], 0) AS CatalogItemCount,
    stats.[CatalogMinPrice],
    stats.[CatalogMaxPrice]"
                + fromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            ExecuteReader(sql, command =>
            {
                AddSupplierFilterParameters(command, organizationId, search);
                SqlQueryHelper.AddParameter(command, "@Skip", skip);
                SqlQueryHelper.AddParameter(command, "@Take", safePageSize);
            }, reader =>
            {
                while (reader.Read())
                {
                    items.Add(MapSupplier(reader));
                }
            });

            return BuildResult(items, totalCount, search, sort, direction, safePage, safePageSize);
        }

        public PagedListVm<AssetCategoryListVm> GetAssetCategoryListPage(
            int organizationId,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildCategoryWhereClause(search);
            var orderBy = BuildCategoryOrderBy(sort, direction);
            var fromClause = @"
FROM [AssetCategory] c
LEFT JOIN (
    SELECT [AssetCategoryId], COUNT(*) AS TypeCount
    FROM [AssetType]
    WHERE [IsActive] = 1
    GROUP BY [AssetCategoryId]
) typeStats ON typeStats.[AssetCategoryId] = c.[Id]
WHERE c.[OrganizationId] = @OrganizationId";

            var totalCount = Count(
                "SELECT COUNT(*) " + fromClause + whereClause,
                command => AddOrganizationSearchParameters(command, organizationId, search));
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = new List<AssetCategoryListVm>();
            var sql = @"
SELECT
    c.[Id],
    c.[Name],
    c.[Description],
    c.[IsActive],
    c.[DefaultUsefulLifeMonths],
    c.[DefaultDepreciationLifeMonths],
    c.[DefaultDepreciationRatePercent],
    ISNULL(typeStats.[TypeCount], 0) AS TypeCount"
                + fromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            ExecuteReader(sql, command =>
            {
                AddOrganizationSearchParameters(command, organizationId, search);
                SqlQueryHelper.AddParameter(command, "@Skip", skip);
                SqlQueryHelper.AddParameter(command, "@Take", safePageSize);
            }, reader =>
            {
                while (reader.Read())
                {
                    items.Add(new AssetCategoryListVm
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        Name = SqlQueryHelper.GetString(reader, "Name"),
                        Description = SqlQueryHelper.GetString(reader, "Description"),
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        DefaultUsefulLifeMonths = GetNullableInt(reader, "DefaultUsefulLifeMonths"),
                        DefaultDepreciationLifeMonths = GetNullableInt(reader, "DefaultDepreciationLifeMonths"),
                        DefaultDepreciationRatePercent = GetNullableDecimal(reader, "DefaultDepreciationRatePercent"),
                        TypeCount = Convert.ToInt32(reader["TypeCount"])
                    });
                }
            });

            return BuildResult(items, totalCount, search, sort, direction, safePage, safePageSize);
        }

        public PagedListVm<AssetTypeListVm> GetAssetTypeListPage(
            int organizationId,
            string search,
            int? categoryId,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildTypeWhereClause(search, categoryId);
            var orderBy = BuildTypeOrderBy(sort, direction);
            var fromClause = @"
FROM [AssetType] t
INNER JOIN [AssetCategory] c ON c.[Id] = t.[AssetCategoryId]
WHERE t.[OrganizationId] = @OrganizationId";

            var totalCount = Count(
                "SELECT COUNT(*) " + fromClause + whereClause,
                command => AddTypeFilterParameters(command, organizationId, search, categoryId));
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = new List<AssetTypeListVm>();
            var sql = @"
SELECT
    t.[Id],
    t.[AssetCategoryId],
    c.[Name] AS CategoryName,
    t.[Name],
    t.[Description],
    t.[IsActive],
    t.[UseCustomUsefulLife],
    t.[UsefulLifeMonths],
    t.[UseCustomDepreciationLife],
    t.[DepreciationLifeMonths],
    t.[UseCustomDepreciationRate],
    t.[DepreciationRatePercent]"
                + fromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            ExecuteReader(sql, command =>
            {
                AddTypeFilterParameters(command, organizationId, search, categoryId);
                SqlQueryHelper.AddParameter(command, "@Skip", skip);
                SqlQueryHelper.AddParameter(command, "@Take", safePageSize);
            }, reader =>
            {
                while (reader.Read())
                {
                    items.Add(new AssetTypeListVm
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        AssetCategoryId = Convert.ToInt32(reader["AssetCategoryId"]),
                        CategoryName = SqlQueryHelper.GetString(reader, "CategoryName"),
                        Name = SqlQueryHelper.GetString(reader, "Name"),
                        Description = SqlQueryHelper.GetString(reader, "Description"),
                        IsActive = Convert.ToBoolean(reader["IsActive"]),
                        UseCustomUsefulLife = Convert.ToBoolean(reader["UseCustomUsefulLife"]),
                        UsefulLifeMonths = GetNullableInt(reader, "UsefulLifeMonths"),
                        UseCustomDepreciationLife = Convert.ToBoolean(reader["UseCustomDepreciationLife"]),
                        DepreciationLifeMonths = GetNullableInt(reader, "DepreciationLifeMonths"),
                        UseCustomDepreciationRate = Convert.ToBoolean(reader["UseCustomDepreciationRate"]),
                        DepreciationRatePercent = GetNullableDecimal(reader, "DepreciationRatePercent")
                    });
                }
            });

            return BuildResult(items, totalCount, search, sort, direction, safePage, safePageSize);
        }

        private static string BuildRoleWhereClause(string search, bool? isActive)
        {
            var clauses = string.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                clauses += " AND (LOWER(r.[Name]) LIKE LOWER(@SearchPattern) OR LOWER(ISNULL(r.[Description], N'')) LIKE LOWER(@SearchPattern))";
            }

            if (isActive.HasValue)
            {
                clauses += " AND r.[IsActive] = @IsActive";
            }

            return clauses;
        }

        private static string BuildRoleOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "status":
                    return desc ? "r.[IsActive] DESC, r.[Name] ASC, r.[Id] ASC" : "r.[IsActive] ASC, r.[Name] ASC, r.[Id] ASC";
                case "system":
                    return desc ? "r.[IsSystemRole] DESC, r.[Name] ASC, r.[Id] ASC" : "r.[IsSystemRole] ASC, r.[Name] ASC, r.[Id] ASC";
                default:
                    return desc ? "r.[Name] DESC, r.[Id] ASC" : "r.[Name] ASC, r.[Id] ASC";
            }
        }

        private static string BuildSupplierWhereClause(string search)
        {
            return string.IsNullOrWhiteSpace(search)
                ? string.Empty
                : " AND (LOWER(s.[SupplierName]) LIKE LOWER(@SearchPattern)"
                  + " OR LOWER(ISNULL(s.[ContactPerson], N'')) LIKE LOWER(@SearchPattern)"
                  + " OR LOWER(ISNULL(s.[Email], N'')) LIKE LOWER(@SearchPattern)"
                  + " OR LOWER(ISNULL(s.[Phone], N'')) LIKE LOWER(@SearchPattern))";
        }

        private static string BuildSupplierOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "contact":
                    return desc ? "s.[ContactPerson] DESC, s.[SupplierName] ASC, s.[Id] ASC" : "s.[ContactPerson] ASC, s.[SupplierName] ASC, s.[Id] ASC";
                case "status":
                    return desc ? "s.[IsActive] DESC, s.[SupplierName] ASC, s.[Id] ASC" : "s.[IsActive] ASC, s.[SupplierName] ASC, s.[Id] ASC";
                default:
                    return desc ? "s.[SupplierName] DESC, s.[Id] ASC" : "s.[SupplierName] ASC, s.[Id] ASC";
            }
        }

        private static string BuildCategoryWhereClause(string search)
        {
            return string.IsNullOrWhiteSpace(search)
                ? string.Empty
                : " AND (LOWER(c.[Name]) LIKE LOWER(@SearchPattern) OR LOWER(ISNULL(c.[Description], N'')) LIKE LOWER(@SearchPattern))";
        }

        private static string BuildCategoryOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "status":
                    return desc ? "c.[IsActive] DESC, c.[Name] ASC, c.[Id] ASC" : "c.[IsActive] ASC, c.[Name] ASC, c.[Id] ASC";
                case "types":
                    return desc ? "ISNULL(typeStats.[TypeCount], 0) DESC, c.[Name] ASC, c.[Id] ASC" : "ISNULL(typeStats.[TypeCount], 0) ASC, c.[Name] ASC, c.[Id] ASC";
                default:
                    return desc ? "c.[Name] DESC, c.[Id] ASC" : "c.[Name] ASC, c.[Id] ASC";
            }
        }

        private static string BuildTypeWhereClause(string search, int? categoryId)
        {
            var clauses = string.Empty;
            if (categoryId.HasValue)
            {
                clauses += " AND t.[AssetCategoryId] = @CategoryId";
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                clauses += " AND (LOWER(t.[Name]) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(t.[Description], N'')) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(c.[Name], N'')) LIKE LOWER(@SearchPattern))";
            }

            return clauses;
        }

        private static string BuildTypeOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "category":
                    return desc ? "c.[Name] DESC, t.[Name] ASC, t.[Id] ASC" : "c.[Name] ASC, t.[Name] ASC, t.[Id] ASC";
                case "status":
                    return desc ? "t.[IsActive] DESC, t.[Name] ASC, t.[Id] ASC" : "t.[IsActive] ASC, t.[Name] ASC, t.[Id] ASC";
                default:
                    return desc ? "t.[Name] DESC, t.[Id] ASC" : "t.[Name] ASC, t.[Id] ASC";
            }
        }

        private static void AddRoleFilterParameters(IDbCommand command, int organizationId, string search, bool? isActive)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)SqlQueryHelper.BuildContainsPattern(search));
            SqlQueryHelper.AddParameter(command, "@IsActive", isActive.HasValue ? (object)(isActive.Value ? 1 : 0) : DBNull.Value);
        }

        private static void AddSupplierFilterParameters(IDbCommand command, int organizationId, string search)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)SqlQueryHelper.BuildContainsPattern(search));
        }

        private static void AddOrganizationSearchParameters(IDbCommand command, int organizationId, string search)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)SqlQueryHelper.BuildContainsPattern(search));
        }

        private static void AddTypeFilterParameters(IDbCommand command, int organizationId, string search, int? categoryId)
        {
            AddOrganizationSearchParameters(command, organizationId, search);
            SqlQueryHelper.AddParameter(command, "@CategoryId", categoryId.HasValue ? (object)categoryId.Value : DBNull.Value);
        }

        private static SupplierVm MapSupplier(IDataRecord reader)
        {
            return new SupplierVm
            {
                Id = Convert.ToInt32(reader["Id"]),
                SupplierName = SqlQueryHelper.GetString(reader, "SupplierName"),
                ContactPerson = SqlQueryHelper.GetString(reader, "ContactPerson"),
                Email = SqlQueryHelper.GetString(reader, "Email"),
                Phone = SqlQueryHelper.GetString(reader, "Phone"),
                Address = SqlQueryHelper.GetString(reader, "Address"),
                RegistrationNumber = SqlQueryHelper.GetString(reader, "RegistrationNumber"),
                TaxId = SqlQueryHelper.GetString(reader, "TaxId"),
                PaymentTerms = SqlQueryHelper.GetString(reader, "PaymentTerms"),
                DefaultLeadTimeDays = GetNullableInt(reader, "DefaultLeadTimeDays"),
                Website = SqlQueryHelper.GetString(reader, "Website"),
                IsPreferred = Convert.ToBoolean(reader["IsPreferred"]),
                Country = SqlQueryHelper.GetString(reader, "Country"),
                PaymentInstructions = SqlQueryHelper.GetString(reader, "PaymentInstructions"),
                Notes = SqlQueryHelper.GetString(reader, "Notes"),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                CatalogItemCount = Convert.ToInt32(reader["CatalogItemCount"]),
                CatalogMinPrice = GetNullableDecimal(reader, "CatalogMinPrice"),
                CatalogMaxPrice = GetNullableDecimal(reader, "CatalogMaxPrice")
            };
        }

        private int Count(string sql, Action<IDbCommand> configure)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    configure(command);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private void ExecuteReader(string sql, Action<IDbCommand> configure, Action<IDataReader> read)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    configure(command);
                    using (var reader = command.ExecuteReader())
                    {
                        read(reader);
                    }
                }
            }
        }

        private static PagedListVm<T> BuildResult<T>(
            IList<T> items,
            int totalCount,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            return new PagedListVm<T>
            {
                Items = items,
                TotalCount = totalCount,
                Search = search,
                Sort = sort,
                Direction = ListPageHelper.NormalizeDirection(direction),
                Page = page,
                PageSize = pageSize
            };
        }

        private static int? GetNullableInt(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }

        private static decimal? GetNullableDecimal(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? (decimal?)null : Convert.ToDecimal(value);
        }
    }
}
