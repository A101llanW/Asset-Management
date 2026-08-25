using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class OperationsQueryRepository : IOperationsQueryRepository
    {
        private const string PurchaseRequestListSql = @"
SELECT
    p.[Id],
    p.[RequestNumber],
    p.[DepartmentId],
    d.[Name] AS DepartmentName,
    p.[RequestedById],
    p.[ApprovalStatus],
    p.[CreatedAt],
    p.[Quantity],
    p.[Currency],
    p.[ItemDescription]
FROM [PurchaseRequest] p
LEFT JOIN [Department] d ON d.[Id] = p.[DepartmentId]
WHERE p.[OrganizationId] = @OrganizationId
  AND p.[IsActive] = 1
  AND (@BypassDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND p.[DepartmentId] = @DepartmentId))
ORDER BY p.[CreatedAt] DESC, p.[Id] DESC";

        private const string PurchaseRecordListSql = @"
SELECT
    pr.[Id],
    pr.[PurchaseRequestId],
    pr.[PurchaseOrderNumber],
    pr.[SupplierId],
    s.[SupplierName],
    pr.[InvoiceNumber],
    pr.[PurchaseDate],
    pr.[Quantity],
    pr.[UnitCost],
    pr.[TotalCost],
    pr.[Currency]
FROM [PurchaseRecord] pr
INNER JOIN [Supplier] s ON s.[Id] = pr.[SupplierId]
WHERE pr.[OrganizationId] = @OrganizationId
  AND pr.[IsActive] = 1
ORDER BY pr.[PurchaseDate] DESC, pr.[Id] DESC";

        private readonly ISqlConnectionFactory _connectionFactory;

        public OperationsQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<PurchaseRequestListItemVm> GetPurchaseRequestList(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            var items = new List<PurchaseRequestListItemVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = PurchaseRequestListSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PurchaseRequestListItemVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                RequestNumber = SqlQueryHelper.GetString(reader, "RequestNumber"),
                                DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                                RequestedById = SqlQueryHelper.GetString(reader, "RequestedById"),
                                ApprovalStatus = ((ApprovalStatus)Convert.ToInt32(reader["ApprovalStatus"])).ToString(),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                Currency = SqlQueryHelper.GetString(reader, "Currency"),
                                ItemDescription = SqlQueryHelper.GetString(reader, "ItemDescription")
                            });
                        }
                    }
                }
            }

            return items;
        }

        public AssetRequestListPageVm GetAssetRequestListPage(
            AssetRequestFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool restrictToOwnDepartment)
        {
            var whereClause = BuildAssetRequestWhereClause(filter, restrictToOwnDepartment);
            var orderBy = BuildAssetRequestOrderBy(sort, direction);
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);

            var totalCount = CountAssetRequests(whereClause, filter, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);
            var skip = (safePage - 1) * safePageSize;

            var items = new List<AssetRequestListVm>();
            var sql = @"
SELECT
    r.[Id],
    r.[RequestedById],
    (u.[FirstName] + N' ' + u.[LastName]) AS RequestedByName,
    d.[Name] AS DepartmentName,
    c.[Name] AS CategoryName,
    r.[RequestedAssetTag],
    ra.[AssetName] AS RequestedAssetName,
    r.[Status],
    r.[CreatedAt]
FROM [AssetRequest] r
LEFT JOIN [Users] u ON u.[Id] = r.[RequestedById]
LEFT JOIN [Department] d ON d.[Id] = r.[DepartmentId]
LEFT JOIN [AssetCategory] c ON c.[Id] = r.[CategoryId]
LEFT JOIN [Asset] ra ON ra.[Id] = r.[RequestedAssetId]
WHERE r.[OrganizationId] = @OrganizationId
  AND r.[IsActive] = 1
  AND (@BypassDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND r.[DepartmentId] = @DepartmentId))"
                + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
                    AddAssetRequestFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", safePageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new AssetRequestListVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                RequestedByName = SqlQueryHelper.GetString(reader, "RequestedByName"),
                                DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                                CategoryName = SqlQueryHelper.GetString(reader, "CategoryName"),
                                RequestedAssetTag = SqlQueryHelper.GetString(reader, "RequestedAssetTag"),
                                RequestedAssetName = SqlQueryHelper.GetString(reader, "RequestedAssetName"),
                                Status = (AssetRequestStatus)Convert.ToInt32(reader["Status"]),
                                CreatedAt = Convert.ToDateTime(reader["CreatedAt"])
                            });
                        }
                    }
                }
            }

            return new AssetRequestListPageVm
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

        public AssignmentListPageVm GetAssignmentListPage(
            AssignmentFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            var whereClause = BuildAssignmentWhereClause(filter);
            var orderBy = BuildAssignmentOrderBy(sort, direction);
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);

            var totalCount = CountAssignments(whereClause, filter, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope);
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);
            var skip = (safePage - 1) * safePageSize;

            var items = new List<AssignmentListVm>();
            var sql = @"
SELECT
    aa.[Id],
    aa.[AssetId],
    a.[AssetTag],
    a.[AssetName],
    aa.[ToUserId],
    (tu.[FirstName] + N' ' + tu.[LastName]) AS ToUserName,
    tu.[Email] AS ToUserEmail,
    td.[Name] AS ToDepartmentName,
    aa.[AssignmentType],
    aa.[AssignedDate],
    aa.[ExpectedReturnDate],
    aa.[RecipientAcknowledged],
    a.[CurrentStatus],
    a.[CurrentCustodianId]
FROM [AssetAssignment] aa
INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId]
LEFT JOIN [Users] tu ON tu.[Id] = aa.[ToUserId]
LEFT JOIN [Department] td ON td.[Id] = aa.[ToDepartmentId]
WHERE aa.[OrganizationId] = @OrganizationId
  AND aa.[IsActive] = 1
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + whereClause
                + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
                    AddAssignmentFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", safePageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var toUserName = (SqlQueryHelper.GetString(reader, "ToUserName") ?? string.Empty).Trim();
                            var toUserEmail = SqlQueryHelper.GetString(reader, "ToUserEmail");
                            if (string.IsNullOrWhiteSpace(toUserName) && !string.IsNullOrWhiteSpace(toUserEmail))
                            {
                                toUserName = toUserEmail;
                            }
                            else if (!string.IsNullOrWhiteSpace(toUserEmail))
                            {
                                toUserName = toUserName + " (" + toUserEmail + ")";
                            }

                            var currentStatus = (AssetStatus)Convert.ToInt32(reader["CurrentStatus"]);
                            var toUserId = SqlQueryHelper.GetString(reader, "ToUserId");
                            var currentCustodianId = SqlQueryHelper.GetString(reader, "CurrentCustodianId");
                            var isCurrent = currentStatus == AssetStatus.Assigned
                                && string.Equals(currentCustodianId, toUserId, StringComparison.OrdinalIgnoreCase);

                            items.Add(new AssignmentListVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                AssetId = Convert.ToInt32(reader["AssetId"]),
                                AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                                AssetName = SqlQueryHelper.GetString(reader, "AssetName"),
                                ToUserName = toUserName,
                                ToDepartmentName = SqlQueryHelper.GetString(reader, "ToDepartmentName"),
                                AssignmentType = ((AssignmentType)Convert.ToInt32(reader["AssignmentType"])).ToString(),
                                AssignedDate = Convert.ToDateTime(reader["AssignedDate"]),
                                ExpectedReturnDate = reader["ExpectedReturnDate"] == DBNull.Value
                                    ? (DateTime?)null
                                    : Convert.ToDateTime(reader["ExpectedReturnDate"]),
                                RecipientAcknowledged = Convert.ToBoolean(reader["RecipientAcknowledged"]),
                                IsCurrentCustody = isCurrent
                            });
                        }
                    }
                }
            }

            return new AssignmentListPageVm
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

        public IList<PurchaseRecordVm> GetPurchaseRecordList(int organizationId)
        {
            var items = new List<PurchaseRecordVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = PurchaseRecordListSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PurchaseRecordVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                PurchaseRequestId = reader["PurchaseRequestId"] == DBNull.Value
                                    ? (int?)null
                                    : Convert.ToInt32(reader["PurchaseRequestId"]),
                                PurchaseOrderNumber = SqlQueryHelper.GetString(reader, "PurchaseOrderNumber"),
                                SupplierId = Convert.ToInt32(reader["SupplierId"]),
                                SupplierName = SqlQueryHelper.GetString(reader, "SupplierName"),
                                InvoiceNumber = SqlQueryHelper.GetString(reader, "InvoiceNumber"),
                                PurchaseDate = Convert.ToDateTime(reader["PurchaseDate"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                UnitCost = Convert.ToDecimal(reader["UnitCost"]),
                                TotalCost = Convert.ToDecimal(reader["TotalCost"]),
                                Currency = SqlQueryHelper.GetString(reader, "Currency")
                            });
                        }
                    }
                }
            }

            return items;
        }

        public bool ExistsActiveSerialNumber(int organizationId, string serialNumber, int? excludeAssetId = null)
        {
            if (string.IsNullOrWhiteSpace(serialNumber))
            {
                return false;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT COUNT(*) FROM [Asset]
WHERE [OrganizationId]=@OrganizationId
  AND [IsActive]=1
  AND [SerialNumber]=@Value
  AND (@ExcludeAssetId IS NULL OR [Id]<>@ExcludeAssetId)";
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@Value", serialNumber.Trim());
                    SqlQueryHelper.AddParameter(
                        command,
                        "@ExcludeAssetId",
                        excludeAssetId.HasValue ? (object)excludeAssetId.Value : DBNull.Value);
                    return Convert.ToInt32(command.ExecuteScalar()) > 0;
                }
            }
        }

        private int CountAssetRequests(
            string extraWhere,
            AssetRequestFilterVm filter,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            var sql = @"
SELECT COUNT(*)
FROM [AssetRequest] r
LEFT JOIN [Asset] ra ON ra.[Id] = r.[RequestedAssetId]
WHERE r.[OrganizationId] = @OrganizationId
  AND r.[IsActive] = 1
  AND (@BypassDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND r.[DepartmentId] = @DepartmentId))"
                + extraWhere;

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
                    AddAssetRequestFilterParameters(command, filter);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private int CountAssignments(
            string extraWhere,
            AssignmentFilterVm filter,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope)
        {
            var sql = @"
SELECT COUNT(*)
FROM [AssetAssignment] aa
INNER JOIN [Asset] a ON a.[Id] = aa.[AssetId]
WHERE aa.[OrganizationId] = @OrganizationId
  AND aa.[IsActive] = 1
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a") + extraWhere;

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
                    AddAssignmentFilterParameters(command, filter);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private static string BuildAssetRequestWhereClause(AssetRequestFilterVm filter, bool restrictToOwnDepartment)
        {
            var clauses = new List<string>();
            if (restrictToOwnDepartment)
            {
                clauses.Add("AND r.[DepartmentId] = @DepartmentId");
            }

            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.Search))
                {
                    clauses.Add("AND (r.[Justification] LIKE @SearchContains OR r.[RequestedAssetTag] LIKE @SearchContains OR ra.[AssetName] LIKE @SearchContains)");
                }

                if (filter.Status.HasValue)
                {
                    clauses.Add("AND r.[Status] = @Status");
                }

                if (filter.DepartmentId.HasValue)
                {
                    clauses.Add("AND r.[DepartmentId] = @FilterDepartmentId");
                }

                if (!string.IsNullOrWhiteSpace(filter.RequestedById))
                {
                    clauses.Add("AND r.[RequestedById] = @RequestedById");
                }
            }

            return string.Join(" ", clauses.ToArray());
        }

        private static void AddAssetRequestFilterParameters(IDbCommand command, AssetRequestFilterVm filter)
        {
            var searchPattern = filter == null || string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : SqlQueryHelper.BuildContainsPattern(filter.Search);
            SqlQueryHelper.AddParameter(command, "@SearchContains", searchPattern ?? (object)DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@Status",
                filter != null && filter.Status.HasValue ? (object)(int)filter.Status.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@FilterDepartmentId",
                filter != null && filter.DepartmentId.HasValue ? (object)filter.DepartmentId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@RequestedById",
                filter == null || string.IsNullOrWhiteSpace(filter.RequestedById) ? (object)DBNull.Value : filter.RequestedById.Trim());
        }

        private static string BuildAssetRequestOrderBy(string sort, string direction)
        {
            var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sort ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "status":
                    return desc ? "r.[Status] DESC, r.[Id] DESC" : "r.[Status] ASC, r.[Id] ASC";
                case "department":
                    return desc ? "d.[Name] DESC, r.[Id] DESC" : "d.[Name] ASC, r.[Id] ASC";
                case "requester":
                    return desc ? "u.[LastName] DESC, r.[Id] DESC" : "u.[LastName] ASC, r.[Id] ASC";
                default:
                    return desc ? "r.[CreatedAt] DESC, r.[Id] DESC" : "r.[CreatedAt] ASC, r.[Id] ASC";
            }
        }

        private static string BuildAssignmentWhereClause(AssignmentFilterVm filter)
        {
            if (filter == null)
            {
                return string.Empty;
            }

            var clauses = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                clauses.Add(@"AND (
    a.[AssetTag] LIKE @SearchContains
    OR a.[AssetName] LIKE @SearchContains
    OR tu.[FirstName] LIKE @SearchContains
    OR tu.[LastName] LIKE @SearchContains
    OR tu.[Email] LIKE @SearchContains
)");
            }

            if (filter.DepartmentId.HasValue)
            {
                clauses.Add("AND (aa.[ToDepartmentId] = @FilterDepartmentId OR a.[DepartmentId] = @FilterDepartmentId)");
            }

            if (!string.IsNullOrWhiteSpace(filter.CustodianUserId))
            {
                clauses.Add("AND (aa.[ToUserId] = @CustodianUserId OR a.[CurrentCustodianId] = @CustodianUserId)");
            }

            if (filter.ActiveOnly == true)
            {
                clauses.Add("AND a.[CurrentStatus] = @AssignedStatus AND a.[CurrentCustodianId] = aa.[ToUserId]");
            }

            return string.Join(" ", clauses.ToArray());
        }

        private static void AddAssignmentFilterParameters(IDbCommand command, AssignmentFilterVm filter)
        {
            var searchPattern = filter == null || string.IsNullOrWhiteSpace(filter.Search)
                ? null
                : SqlQueryHelper.BuildContainsPattern(filter.Search);
            SqlQueryHelper.AddParameter(command, "@SearchContains", searchPattern ?? (object)DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@FilterDepartmentId",
                filter != null && filter.DepartmentId.HasValue ? (object)filter.DepartmentId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@CustodianUserId",
                filter == null || string.IsNullOrWhiteSpace(filter.CustodianUserId) ? (object)DBNull.Value : filter.CustodianUserId.Trim());
            SqlQueryHelper.AddParameter(command, "@AssignedStatus", (int)AssetStatus.Assigned);
        }

        private static string BuildAssignmentOrderBy(string sort, string direction)
        {
            var desc = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
            switch ((sort ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "asset":
                    return desc ? "a.[AssetTag] DESC, aa.[Id] DESC" : "a.[AssetTag] ASC, aa.[Id] ASC";
                case "user":
                    return desc ? "tu.[LastName] DESC, aa.[Id] DESC" : "tu.[LastName] ASC, aa.[Id] ASC";
                case "department":
                    return desc ? "td.[Name] DESC, aa.[Id] DESC" : "td.[Name] ASC, aa.[Id] ASC";
                case "type":
                    return desc ? "aa.[AssignmentType] DESC, aa.[Id] DESC" : "aa.[AssignmentType] ASC, aa.[Id] ASC";
                default:
                    return desc ? "aa.[AssignedDate] DESC, aa.[Id] DESC" : "aa.[AssignedDate] ASC, aa.[Id] ASC";
            }
        }

        public PagedListVm<PurchaseRequestListItemVm> GetPurchaseRequestListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildPurchaseRequestWhereClause(search);
            var orderBy = BuildPurchaseRequestOrderBy(sort, direction);
            var fromClause = @"
FROM [PurchaseRequest] p
LEFT JOIN [Department] d ON d.[Id] = p.[DepartmentId]
WHERE p.[OrganizationId] = @OrganizationId
  AND p.[IsActive] = 1
  AND (@BypassDepartmentScope = 1 OR (@DenyDepartmentScope = 0 AND @DepartmentId IS NOT NULL AND p.[DepartmentId] = @DepartmentId))";

            var totalCount = CountPurchaseRequests(fromClause + whereClause, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search);
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = new List<PurchaseRequestListItemVm>();
            var sql = @"
SELECT
    p.[Id],
    p.[RequestNumber],
    p.[DepartmentId],
    d.[Name] AS DepartmentName,
    p.[RequestedById],
    p.[ApprovalStatus],
    p.[CreatedAt],
    p.[Quantity],
    p.[Currency],
    p.[ItemDescription]"
                + fromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddPurchaseRequestScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", safePageSize);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapPurchaseRequest(reader));
                        }
                    }
                }
            }

            return BuildPagedResult(items, totalCount, search, sort, direction, safePage, safePageSize);
        }

        public PagedListVm<PurchaseRecordVm> GetPurchaseRecordListPage(
            int organizationId,
            string search,
            int? supplierId,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildPurchaseRecordWhereClause(search, supplierId);
            var orderBy = BuildPurchaseRecordOrderBy(sort, direction);
            var fromClause = @"
FROM [PurchaseRecord] pr
INNER JOIN [Supplier] s ON s.[Id] = pr.[SupplierId]
WHERE pr.[OrganizationId] = @OrganizationId
  AND pr.[IsActive] = 1";

            var totalCount = CountPurchaseRecords(fromClause + whereClause, organizationId, search, supplierId);
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = new List<PurchaseRecordVm>();
            var sql = @"
SELECT
    pr.[Id],
    pr.[PurchaseRequestId],
    pr.[PurchaseOrderNumber],
    pr.[SupplierId],
    s.[SupplierName],
    pr.[InvoiceNumber],
    pr.[PurchaseDate],
    pr.[Quantity],
    pr.[UnitCost],
    pr.[TotalCost],
    pr.[Currency]"
                + fromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddPurchaseRecordFilterParameters(command, organizationId, search, supplierId);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", safePageSize);
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new PurchaseRecordVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                PurchaseRequestId = reader["PurchaseRequestId"] == DBNull.Value
                                    ? (int?)null
                                    : Convert.ToInt32(reader["PurchaseRequestId"]),
                                PurchaseOrderNumber = SqlQueryHelper.GetString(reader, "PurchaseOrderNumber"),
                                SupplierId = Convert.ToInt32(reader["SupplierId"]),
                                SupplierName = SqlQueryHelper.GetString(reader, "SupplierName"),
                                InvoiceNumber = SqlQueryHelper.GetString(reader, "InvoiceNumber"),
                                PurchaseDate = Convert.ToDateTime(reader["PurchaseDate"]),
                                Quantity = Convert.ToInt32(reader["Quantity"]),
                                UnitCost = Convert.ToDecimal(reader["UnitCost"]),
                                TotalCost = Convert.ToDecimal(reader["TotalCost"]),
                                Currency = SqlQueryHelper.GetString(reader, "Currency")
                            });
                        }
                    }
                }
            }

            return BuildPagedResult(items, totalCount, search, sort, direction, safePage, safePageSize);
        }

        public PagedListVm<IncidentListVm> GetIncidentListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            int? assetId,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildAssetLinkedWhereClause(true, search, assetId);
            var fromClause = @"
FROM [AssetIncident] i
INNER JOIN [Asset] a ON a.[Id] = i.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a");

            var totalCount = CountAssetLinked(fromClause + whereClause, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search, assetId);
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var sql = @"
SELECT
    i.[Id],
    i.[IncidentNumber],
    i.[AssetId],
    a.[AssetTag],
    a.[AssetName],
    i.[IncidentType],
    i.[Severity],
    i.[IncidentDate],
    i.[ResolutionStatus]"
                + fromClause + whereClause + " ORDER BY i.[IncidentDate] DESC, i.[Id] DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            var items = new List<IncidentListVm>();
            ExecuteAssetLinkedQuery(sql, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search, assetId, skip, safePageSize, reader =>
            {
                while (reader.Read())
                {
                    items.Add(new IncidentListVm
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        IncidentNumber = SqlQueryHelper.GetString(reader, "IncidentNumber"),
                        AssetId = Convert.ToInt32(reader["AssetId"]),
                        AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                        AssetName = SqlQueryHelper.GetString(reader, "AssetName"),
                        IncidentType = ((IncidentType)Convert.ToInt32(reader["IncidentType"])).ToString(),
                        Severity = (IncidentSeverity)Convert.ToInt32(reader["Severity"]),
                        IncidentDate = Convert.ToDateTime(reader["IncidentDate"]),
                        ResolutionStatus = SqlQueryHelper.GetString(reader, "ResolutionStatus")
                    });
                }
            });

            return BuildPagedResult(items, totalCount, search, null, "desc", safePage, safePageSize);
        }

        public PagedListVm<ClaimListVm> GetClaimListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            int? assetId,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildAssetLinkedWhereClause(false, search, assetId);
            var fromClause = @"
FROM [InsuranceClaim] c
INNER JOIN [Asset] a ON a.[Id] = c.[AssetId]
WHERE a.[OrganizationId] = @OrganizationId
  AND a.[IsActive] = 1
  AND " + SqlQueryHelper.FormatAssetDepartmentScopeSql("a");

            var totalCount = CountAssetLinked(fromClause + whereClause, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search, assetId);
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var sql = @"
SELECT
    c.[Id],
    c.[ClaimNumber],
    c.[AssetId],
    a.[AssetTag],
    a.[AssetName],
    c.[ClaimType],
    c.[Insurer],
    c.[ClaimStatus],
    c.[ClaimDate]"
                + fromClause + whereClause + " ORDER BY c.[ClaimDate] DESC, c.[Id] DESC OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            var claims = new List<ClaimListVm>();
            ExecuteAssetLinkedQuery(sql, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search, assetId, skip, safePageSize, reader =>
            {
                while (reader.Read())
                {
                    claims.Add(new ClaimListVm
                    {
                        Id = Convert.ToInt32(reader["Id"]),
                        ClaimNumber = SqlQueryHelper.GetString(reader, "ClaimNumber"),
                        AssetId = Convert.ToInt32(reader["AssetId"]),
                        AssetTag = SqlQueryHelper.GetString(reader, "AssetTag"),
                        AssetName = SqlQueryHelper.GetString(reader, "AssetName"),
                        ClaimType = SqlQueryHelper.GetString(reader, "ClaimType"),
                        Insurer = SqlQueryHelper.GetString(reader, "Insurer"),
                        ClaimStatus = (ClaimStatus)Convert.ToInt32(reader["ClaimStatus"]),
                        ClaimDate = Convert.ToDateTime(reader["ClaimDate"])
                    });
                }
            });

            return BuildPagedResult(claims, totalCount, search, null, "desc", safePage, safePageSize);
        }

        private static string BuildPurchaseRequestWhereClause(string search)
        {
            return string.IsNullOrWhiteSpace(search)
                ? string.Empty
                : " AND (LOWER(p.[RequestNumber]) LIKE LOWER(@SearchPattern)"
                  + " OR LOWER(ISNULL(p.[ItemDescription], N'')) LIKE LOWER(@SearchPattern)"
                  + " OR LOWER(ISNULL(d.[Name], N'')) LIKE LOWER(@SearchPattern))";
        }

        private static string BuildPurchaseRequestOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "department":
                    return desc ? "d.[Name] DESC, p.[Id] DESC" : "d.[Name] ASC, p.[Id] ASC";
                case "status":
                    return desc ? "p.[ApprovalStatus] DESC, p.[Id] DESC" : "p.[ApprovalStatus] ASC, p.[Id] ASC";
                default:
                    return desc ? "p.[CreatedAt] DESC, p.[Id] DESC" : "p.[CreatedAt] ASC, p.[Id] ASC";
            }
        }

        private static string BuildPurchaseRecordWhereClause(string search, int? supplierId)
        {
            var clauses = string.Empty;
            if (!string.IsNullOrWhiteSpace(search))
            {
                clauses += " AND (LOWER(pr.[PurchaseOrderNumber]) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(pr.[InvoiceNumber], N'')) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(s.[SupplierName], N'')) LIKE LOWER(@SearchPattern))";
            }

            if (supplierId.HasValue)
            {
                clauses += " AND pr.[SupplierId] = @SupplierId";
            }

            return clauses;
        }

        private static string BuildPurchaseRecordOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "supplier":
                    return desc ? "s.[SupplierName] DESC, pr.[Id] DESC" : "s.[SupplierName] ASC, pr.[Id] ASC";
                case "total":
                    return desc ? "pr.[TotalCost] DESC, pr.[Id] DESC" : "pr.[TotalCost] ASC, pr.[Id] ASC";
                default:
                    return desc ? "pr.[PurchaseDate] DESC, pr.[Id] DESC" : "pr.[PurchaseDate] ASC, pr.[Id] ASC";
            }
        }

        private static string BuildAssetLinkedWhereClause(bool isIncident, string search, int? assetId)
        {
            var clauses = string.Empty;
            if (assetId.HasValue)
            {
                clauses += " AND a.[Id] = @AssetId";
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                if (isIncident)
                {
                    clauses += " AND (LOWER(a.[AssetTag]) LIKE LOWER(@SearchPattern)"
                        + " OR LOWER(ISNULL(a.[AssetName], N'')) LIKE LOWER(@SearchPattern)"
                        + " OR LOWER(ISNULL(i.[IncidentNumber], N'')) LIKE LOWER(@SearchPattern)"
                        + " OR LOWER(ISNULL(i.[Description], N'')) LIKE LOWER(@SearchPattern))";
                }
                else
                {
                    clauses += " AND (LOWER(a.[AssetTag]) LIKE LOWER(@SearchPattern)"
                        + " OR LOWER(ISNULL(a.[AssetName], N'')) LIKE LOWER(@SearchPattern)"
                        + " OR LOWER(ISNULL(c.[ClaimNumber], N'')) LIKE LOWER(@SearchPattern)"
                        + " OR LOWER(ISNULL(c.[Insurer], N'')) LIKE LOWER(@SearchPattern))";
                }
            }

            return clauses;
        }

        private int CountPurchaseRequests(
            string sql,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*)" + sql;
                    AddPurchaseRequestScopeParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private int CountPurchaseRecords(string sql, int organizationId, string search, int? supplierId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*)" + sql;
                    AddPurchaseRecordFilterParameters(command, organizationId, search, supplierId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private int CountAssetLinked(
            string sql,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            int? assetId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*)" + sql;
                    AddAssetLinkedParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search, assetId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private void ExecuteAssetLinkedQuery(
            string sql,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            int? assetId,
            int skip,
            int take,
            Action<IDataReader> read)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddAssetLinkedParameters(command, organizationId, departmentId, bypassDepartmentScope, denyDepartmentScope, search, assetId);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", take);
                    using (var reader = command.ExecuteReader())
                    {
                        read(reader);
                    }
                }
            }
        }

        private static void AddPurchaseRequestScopeParameters(
            IDbCommand command,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)SqlQueryHelper.BuildContainsPattern(search));
        }

        private static void AddPurchaseRecordFilterParameters(IDbCommand command, int organizationId, string search, int? supplierId)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)SqlQueryHelper.BuildContainsPattern(search));
            SqlQueryHelper.AddParameter(command, "@SupplierId", supplierId.HasValue ? (object)supplierId.Value : DBNull.Value);
        }

        private static void AddAssetLinkedParameters(
            IDbCommand command,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            int? assetId)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddDepartmentScopeParameters(command, bypassDepartmentScope, denyDepartmentScope, departmentId);
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                string.IsNullOrWhiteSpace(search) ? DBNull.Value : (object)SqlQueryHelper.BuildContainsPattern(search));
            SqlQueryHelper.AddParameter(command, "@AssetId", assetId.HasValue ? (object)assetId.Value : DBNull.Value);
        }

        private static PurchaseRequestListItemVm MapPurchaseRequest(IDataRecord reader)
        {
            return new PurchaseRequestListItemVm
            {
                Id = Convert.ToInt32(reader["Id"]),
                RequestNumber = SqlQueryHelper.GetString(reader, "RequestNumber"),
                DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                RequestedById = SqlQueryHelper.GetString(reader, "RequestedById"),
                ApprovalStatus = ((ApprovalStatus)Convert.ToInt32(reader["ApprovalStatus"])).ToString(),
                CreatedAt = Convert.ToDateTime(reader["CreatedAt"]),
                Quantity = Convert.ToInt32(reader["Quantity"]),
                Currency = SqlQueryHelper.GetString(reader, "Currency"),
                ItemDescription = SqlQueryHelper.GetString(reader, "ItemDescription")
            };
        }

        private static PagedListVm<T> BuildPagedResult<T>(
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
    }
}
