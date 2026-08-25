using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.ViewModels.Platform;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Queries;

namespace AssetManagement.Infrastructure.Identity
{
    public class UserAccountQueryRepository : IUserAccountQueryRepository
    {
        private const string UsersListSql = @"
SELECT
    u.[Id],
    u.[EmployeeNumber],
    u.[FirstName],
    u.[LastName],
    u.[Email],
    u.[Phone],
    u.[DepartmentId],
    d.[Name] AS DepartmentName,
    u.[PositionTitle],
    u.[IsActive],
    u.[RoleId],
    r.[Name] AS RoleName
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
    AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
WHERE u.[OrganizationId] = @OrganizationId
  AND (@DepartmentId IS NULL OR u.[DepartmentId] = @DepartmentId)
ORDER BY u.[FirstName], u.[LastName], u.[Id]";

        private const string UserByIdSql = @"
SELECT
    u.[Id],
    u.[EmployeeNumber],
    u.[FirstName],
    u.[LastName],
    u.[Email],
    u.[Phone],
    u.[DepartmentId],
    d.[Name] AS DepartmentName,
    u.[PositionTitle],
    u.[IsActive],
    u.[RoleId],
    r.[Name] AS RoleName
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
    AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
WHERE u.[Id] = @Id
  AND u.[OrganizationId] = @OrganizationId";

        private const string DisplaySql = @"
SELECT
    u.[Id],
    u.[FirstName],
    u.[LastName],
    u.[Email],
    u.[RoleId]
FROM [Users] u
WHERE u.[Id] = @Id
  AND (@OrganizationId IS NULL OR u.[OrganizationId] = @OrganizationId)";

        private const string AllUsersForPlatformSql = @"
SELECT
    u.[Id],
    u.[EmployeeNumber],
    u.[FirstName],
    u.[LastName],
    u.[Email],
    u.[Phone],
    u.[DepartmentId],
    d.[Name] AS DepartmentName,
    u.[PositionTitle],
    u.[IsActive],
    u.[RoleId],
    r.[Name] AS RoleName,
    u.[OrganizationId],
    o.[Name] AS OrganizationName
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
    AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
LEFT JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
ORDER BY o.[Name], u.[FirstName], u.[LastName], u.[Id]";

        private const string UserByIdForPlatformSql = @"
SELECT
    u.[Id],
    u.[EmployeeNumber],
    u.[FirstName],
    u.[LastName],
    u.[Email],
    u.[Phone],
    u.[DepartmentId],
    d.[Name] AS DepartmentName,
    u.[PositionTitle],
    u.[IsActive],
    u.[RoleId],
    r.[Name] AS RoleName,
    u.[OrganizationId],
    o.[Name] AS OrganizationName
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
    AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
LEFT JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
WHERE u.[Id] = @Id";

        private const string PlatformRolesSql = @"
SELECT [Id], [Name], [Description], [IsSystemRole], [IsActive]
FROM [Roles]
WHERE [OrganizationId] IS NULL AND [IsActive] = 1
ORDER BY [Name], [Id]";

        private readonly ISqlConnectionFactory _connectionFactory;

        public UserAccountQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<UserVm> GetUsersForOrganization(int organizationId, int? departmentId, bool bypassDepartmentScope)
        {
            var items = new List<UserVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = UsersListSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@DepartmentId",
                        !bypassDepartmentScope && departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new UserVm
                            {
                                Id = SqlQueryHelper.GetString(reader, "Id"),
                                EmployeeNumber = SqlQueryHelper.GetString(reader, "EmployeeNumber"),
                                FirstName = SqlQueryHelper.GetString(reader, "FirstName"),
                                LastName = SqlQueryHelper.GetString(reader, "LastName"),
                                Email = SqlQueryHelper.GetString(reader, "Email"),
                                Phone = SqlQueryHelper.GetString(reader, "Phone"),
                                DepartmentId = GetNullableInt(reader, "DepartmentId"),
                                DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                                PositionTitle = SqlQueryHelper.GetString(reader, "PositionTitle"),
                                IsActive = Convert.ToBoolean(reader["IsActive"]),
                                RoleId = GetNullableInt(reader, "RoleId"),
                                RoleName = SqlQueryHelper.GetString(reader, "RoleName")
                            });
                        }
                    }
                }
            }

            return items;
        }

        public UserVm GetUserById(string userId, int organizationId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = UserByIdSql;
                    SqlQueryHelper.AddParameter(command, "@Id", userId.Trim());
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return new UserVm
                        {
                            Id = SqlQueryHelper.GetString(reader, "Id"),
                            EmployeeNumber = SqlQueryHelper.GetString(reader, "EmployeeNumber"),
                            FirstName = SqlQueryHelper.GetString(reader, "FirstName"),
                            LastName = SqlQueryHelper.GetString(reader, "LastName"),
                            Email = SqlQueryHelper.GetString(reader, "Email"),
                            Phone = SqlQueryHelper.GetString(reader, "Phone"),
                            DepartmentId = GetNullableInt(reader, "DepartmentId"),
                            DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                            PositionTitle = SqlQueryHelper.GetString(reader, "PositionTitle"),
                            IsActive = Convert.ToBoolean(reader["IsActive"]),
                            RoleId = GetNullableInt(reader, "RoleId"),
                            RoleName = SqlQueryHelper.GetString(reader, "RoleName")
                        };
                    }
                }
            }
        }

        public UserDisplayProjection GetDisplayById(string userId, int? organizationId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = DisplaySql;
                    SqlQueryHelper.AddParameter(command, "@Id", userId.Trim());
                    SqlQueryHelper.AddParameter(command, "@OrganizationId",
                        organizationId.HasValue ? (object)organizationId.Value : DBNull.Value);

                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        var firstName = SqlQueryHelper.GetString(reader, "FirstName");
                        var lastName = SqlQueryHelper.GetString(reader, "LastName");
                        var email = SqlQueryHelper.GetString(reader, "Email");
                        var displayName = (firstName + " " + lastName).Trim();
                        if (string.IsNullOrWhiteSpace(displayName))
                        {
                            displayName = email;
                        }

                        return new UserDisplayProjection
                        {
                            Id = SqlQueryHelper.GetString(reader, "Id"),
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            RoleId = GetNullableInt(reader, "RoleId"),
                            DisplayName = displayName
                        };
                    }
                }
            }
        }

        public int? GetRoleIdByUserId(string userId)
        {
            var display = GetDisplayById(userId, null);
            return display == null ? null : display.RoleId;
        }

        public int CountUsersForOrganization(int organizationId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM [Users] WHERE [OrganizationId] = @OrganizationId";
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public int CountUsersForRole(int organizationId, int roleId)
        {
            return CountUsers("[OrganizationId] = @OrganizationId AND [RoleId] = @RoleId", command =>
            {
                SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                SqlQueryHelper.AddParameter(command, "@RoleId", roleId);
            });
        }

        public int CountActiveUsersForDepartment(int organizationId, int departmentId)
        {
            return CountUsers("[OrganizationId] = @OrganizationId AND [DepartmentId] = @DepartmentId AND [IsActive] = 1", command =>
            {
                SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                SqlQueryHelper.AddParameter(command, "@DepartmentId", departmentId);
            });
        }

        private int CountUsers(string whereClause, Action<IDbCommand> addParameters)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM [Users] WHERE " + whereClause;
                    addParameters(command);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public IList<PlatformUserListItemVm> GetAllUsersForPlatformAdmin()
        {
            var items = new List<PlatformUserListItemVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = AllUsersForPlatformSql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapPlatformUser(reader));
                        }
                    }
                }
            }

            return items;
        }

        public PlatformUserListItemVm GetUserByIdForPlatform(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = UserByIdForPlatformSql;
                    SqlQueryHelper.AddParameter(command, "@Id", userId.Trim());
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapPlatformUser(reader) : null;
                    }
                }
            }
        }

        public IList<RoleVm> GetPlatformRoles()
        {
            var items = new List<RoleVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = PlatformRolesSql;
                    using (var reader = command.ExecuteReader())
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
                    }
                }
            }

            return items;
        }

        public PagedListVm<UserVm> GetUserListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            UserListFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildUserWhereClause(filter);
            var orderBy = BuildUserOrderBy(sort, direction);
            var scopeDepartmentId = !bypassDepartmentScope && departmentId.HasValue ? departmentId : filter?.DepartmentId;

            var fromClause = @"
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
    AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
WHERE u.[OrganizationId] = @OrganizationId
  AND (@ScopeDepartmentId IS NULL OR u.[DepartmentId] = @ScopeDepartmentId)";

            var totalCount = CountUsers(fromClause + whereClause, organizationId, scopeDepartmentId, filter);
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = new List<UserVm>();
            var sql = @"
SELECT
    u.[Id],
    u.[EmployeeNumber],
    u.[FirstName],
    u.[LastName],
    u.[Email],
    u.[Phone],
    u.[DepartmentId],
    d.[Name] AS DepartmentName,
    u.[PositionTitle],
    u.[IsActive],
    u.[RoleId],
    r.[Name] AS RoleName"
                + fromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            ExecuteUserQuery(sql, organizationId, scopeDepartmentId, filter, command =>
            {
                SqlQueryHelper.AddParameter(command, "@Skip", skip);
                SqlQueryHelper.AddParameter(command, "@Take", safePageSize);
            }, reader =>
            {
                while (reader.Read())
                {
                    items.Add(MapUser(reader));
                }
            });

            return new PagedListVm<UserVm>
            {
                Items = items,
                TotalCount = totalCount,
                Search = filter?.Search,
                Sort = sort,
                Direction = ListPageHelper.NormalizeDirection(direction),
                Page = safePage,
                PageSize = safePageSize
            };
        }

        public PagedListVm<PlatformUserListItemVm> GetPlatformUserListPage(
            PlatformUserListFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var whereClause = BuildPlatformWhereClause(filter, null);
            var orderBy = BuildPlatformOrderBy(sort, direction);
            var fromClause = PlatformFromClause;

            var totalCount = CountPlatformUsers(fromClause + whereClause, filter);
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, safePageSize, totalCount, out safePage);

            var items = QueryPlatformUsers(fromClause + whereClause + " ORDER BY " + orderBy + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY", filter, skip, safePageSize);

            return new PagedListVm<PlatformUserListItemVm>
            {
                Items = items,
                TotalCount = totalCount,
                Search = filter?.Search,
                Sort = sort,
                Direction = ListPageHelper.NormalizeDirection(direction),
                Page = safePage,
                PageSize = safePageSize
            };
        }

        public PlatformUserIndexViewModel GetPlatformUserIndexPage(
            PlatformUserListFilterVm filter,
            string sort,
            string direction,
            string category,
            int page,
            int pageSize)
        {
            var safePageSize = ListPageHelper.NormalizePageSize(pageSize);
            var normalizedCategory = NormalizePlatformCategory(category, filter?.UserScope);
            var baseWhere = BuildPlatformWhereClause(filter, null);
            var fromClause = PlatformFromClause;

            var viewModel = new PlatformUserIndexViewModel
            {
                Search = filter?.Search,
                OrganizationId = filter?.OrganizationId,
                UserScope = filter?.UserScope,
                RoleId = filter?.RoleId,
                IsActive = filter?.IsActive,
                Sort = sort,
                Direction = ListPageHelper.NormalizeDirection(direction),
                Category = normalizedCategory,
                Page = page,
                PageSize = safePageSize,
                SystemUserCount = CountPlatformUsers(fromClause + baseWhere + " AND u.[OrganizationId] IS NULL", filter),
                OrganizationAdminCount = CountPlatformUsers(fromClause + baseWhere + PlatformAdminPredicate, filter),
                OrganizationCount = CountPlatformOrganizations(fromClause + baseWhere + PlatformTenantPredicate, filter),
                TotalCount = CountPlatformUsers(fromClause + baseWhere, filter)
            };

            switch (normalizedCategory)
            {
                case "admins":
                    int adminPage;
                    var adminSkip = ListPageHelper.ComputeSkip(page, safePageSize, viewModel.OrganizationAdminCount, out adminPage);
                    viewModel.OrganizationAdmins = QueryPlatformUsers(
                        fromClause + baseWhere + PlatformAdminPredicate + " ORDER BY " + BuildPlatformOrderBy(sort, direction) + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                        filter,
                        adminSkip,
                        safePageSize);
                    ApplyActivePagination(viewModel, adminPage, safePageSize, viewModel.OrganizationAdminCount);
                    break;
                case "tenant":
                    PaginatePlatformOrganizationGroups(fromClause, baseWhere, filter, sort, direction, page, safePageSize, viewModel);
                    break;
                default:
                    int systemPage;
                    var systemSkip = ListPageHelper.ComputeSkip(page, safePageSize, viewModel.SystemUserCount, out systemPage);
                    viewModel.SystemUsers = QueryPlatformUsers(
                        fromClause + baseWhere + " AND u.[OrganizationId] IS NULL ORDER BY " + BuildPlatformOrderBy(sort, direction) + " OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY",
                        filter,
                        systemSkip,
                        safePageSize);
                    viewModel.Category = "system";
                    ApplyActivePagination(viewModel, systemPage, safePageSize, viewModel.SystemUserCount);
                    break;
            }

            return viewModel;
        }

        private const string PlatformFromClause = @"
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
LEFT JOIN [Roles] r ON r.[Id] = u.[RoleId]
    AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
LEFT JOIN [Organization] o ON o.[Id] = u.[OrganizationId]
WHERE 1 = 1";

        private const string PlatformAdminPredicate =
            " AND u.[OrganizationId] IS NOT NULL AND LOWER(ISNULL(r.[Name], N'')) = N'company admin'";

        private const string PlatformTenantPredicate =
            " AND u.[OrganizationId] IS NOT NULL AND LOWER(ISNULL(r.[Name], N'')) <> N'company admin'";

        private static string BuildUserWhereClause(UserListFilterVm filter)
        {
            if (filter == null)
            {
                return string.Empty;
            }

            var clauses = string.Empty;
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                clauses += " AND (LOWER(LTRIM(RTRIM(ISNULL(u.[FirstName], N'') + N' ' + ISNULL(u.[LastName], N'')))) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(u.[Email], N'')) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(u.[EmployeeNumber], N'')) LIKE LOWER(@SearchPattern))";
            }

            if (filter.RoleId.HasValue)
            {
                clauses += " AND u.[RoleId] = @RoleId";
            }

            if (filter.DepartmentId.HasValue)
            {
                clauses += " AND u.[DepartmentId] = @FilterDepartmentId";
            }

            if (filter.IsActive.HasValue)
            {
                clauses += " AND u.[IsActive] = @IsActive";
            }

            return clauses;
        }

        private static string BuildUserOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "email":
                    return desc ? "u.[Email] DESC, u.[Id] ASC" : "u.[Email] ASC, u.[Id] ASC";
                case "role":
                    return desc ? "r.[Name] DESC, u.[Id] ASC" : "r.[Name] ASC, u.[Id] ASC";
                case "status":
                    return desc ? "u.[IsActive] DESC, u.[Id] ASC" : "u.[IsActive] ASC, u.[Id] ASC";
                default:
                    return desc
                        ? "u.[FirstName] DESC, u.[LastName] DESC, u.[Id] ASC"
                        : "u.[FirstName] ASC, u.[LastName] ASC, u.[Id] ASC";
            }
        }

        private static string BuildPlatformWhereClause(PlatformUserListFilterVm filter, string categoryOverride)
        {
            var clauses = string.Empty;
            if (filter == null)
            {
                return clauses;
            }

            if (string.Equals(filter.UserScope, "system", StringComparison.OrdinalIgnoreCase))
            {
                clauses += " AND u.[OrganizationId] IS NULL";
            }
            else if (string.Equals(filter.UserScope, "company", StringComparison.OrdinalIgnoreCase))
            {
                clauses += " AND u.[OrganizationId] IS NOT NULL";
            }

            if (filter.OrganizationId.HasValue)
            {
                clauses += " AND u.[OrganizationId] = @OrganizationFilterId";
            }

            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                clauses += " AND (LOWER(LTRIM(RTRIM(ISNULL(u.[FirstName], N'') + N' ' + ISNULL(u.[LastName], N'')))) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(u.[Email], N'')) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(u.[EmployeeNumber], N'')) LIKE LOWER(@SearchPattern)"
                    + " OR LOWER(ISNULL(o.[Name], N'')) LIKE LOWER(@SearchPattern))";
            }

            if (filter.RoleId.HasValue)
            {
                clauses += " AND u.[RoleId] = @RoleId";
            }

            if (filter.IsActive.HasValue)
            {
                clauses += " AND u.[IsActive] = @IsActive";
            }

            if (!string.IsNullOrWhiteSpace(categoryOverride))
            {
                switch (categoryOverride.ToLowerInvariant())
                {
                    case "system":
                        clauses += " AND u.[OrganizationId] IS NULL";
                        break;
                    case "admins":
                        clauses += PlatformAdminPredicate;
                        break;
                    case "tenant":
                        clauses += PlatformTenantPredicate;
                        break;
                }
            }

            return clauses;
        }

        private static string BuildPlatformOrderBy(string sort, string direction)
        {
            var desc = ListPageHelper.NormalizeDirection(direction) == "desc";
            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "email":
                    return desc ? "u.[Email] DESC, u.[Id] ASC" : "u.[Email] ASC, u.[Id] ASC";
                case "organization":
                    return desc ? "o.[Name] DESC, u.[Id] ASC" : "o.[Name] ASC, u.[Id] ASC";
                case "role":
                    return desc ? "r.[Name] DESC, u.[Id] ASC" : "r.[Name] ASC, u.[Id] ASC";
                case "status":
                    return desc ? "u.[IsActive] DESC, u.[Id] ASC" : "u.[IsActive] ASC, u.[Id] ASC";
                default:
                    return desc
                        ? "u.[FirstName] DESC, u.[LastName] DESC, u.[Id] ASC"
                        : "u.[FirstName] ASC, u.[LastName] ASC, u.[Id] ASC";
            }
        }

        private static string NormalizePlatformCategory(string category, string userScope)
        {
            if (string.Equals(userScope, "system", StringComparison.OrdinalIgnoreCase))
            {
                return "system";
            }

            if (string.Equals(userScope, "company", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(category) || string.Equals(category, "system", StringComparison.OrdinalIgnoreCase))
                {
                    return "tenant";
                }

                return category.Trim().ToLowerInvariant();
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return "system";
            }

            var normalized = category.Trim().ToLowerInvariant();
            return normalized == "system" || normalized == "admins" || normalized == "tenant"
                ? normalized
                : "system";
        }

        private int CountUsers(string sql, int organizationId, int? scopeDepartmentId, UserListFilterVm filter)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*)" + sql;
                    AddUserFilterParameters(command, organizationId, scopeDepartmentId, filter);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private void ExecuteUserQuery(
            string sql,
            int organizationId,
            int? scopeDepartmentId,
            UserListFilterVm filter,
            Action<IDbCommand> configure,
            Action<IDataReader> read)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    AddUserFilterParameters(command, organizationId, scopeDepartmentId, filter);
                    configure?.Invoke(command);
                    using (var reader = command.ExecuteReader())
                    {
                        read(reader);
                    }
                }
            }
        }

        private static void AddUserFilterParameters(IDbCommand command, int organizationId, int? scopeDepartmentId, UserListFilterVm filter)
        {
            SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
            SqlQueryHelper.AddParameter(command, "@ScopeDepartmentId", scopeDepartmentId.HasValue ? (object)scopeDepartmentId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                filter == null || string.IsNullOrWhiteSpace(filter.Search)
                    ? DBNull.Value
                    : (object)SqlQueryHelper.BuildContainsPattern(filter.Search));
            SqlQueryHelper.AddParameter(command, "@RoleId", filter?.RoleId.HasValue == true ? (object)filter.RoleId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@FilterDepartmentId", filter?.DepartmentId.HasValue == true ? (object)filter.DepartmentId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@IsActive", filter?.IsActive.HasValue == true ? (object)(filter.IsActive.Value ? 1 : 0) : DBNull.Value);
        }

        private int CountPlatformUsers(string sql, PlatformUserListFilterVm filter)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*)" + sql;
                    AddPlatformFilterParameters(command, filter);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private int CountPlatformOrganizations(string sql, PlatformUserListFilterVm filter)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(DISTINCT u.[OrganizationId])" + sql;
                    AddPlatformFilterParameters(command, filter);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private IList<PlatformUserListItemVm> QueryPlatformUsersForOrganization(
            string fromClause,
            string baseWhere,
            PlatformUserListFilterVm filter,
            int organizationId,
            string sort,
            string direction)
        {
            return QueryPlatformUsers(
                fromClause + baseWhere + PlatformTenantPredicate + " AND u.[OrganizationId] = @TenantOrganizationId ORDER BY " + BuildPlatformOrderBy(sort, direction),
                filter,
                0,
                0,
                organizationId);
        }

        private IList<PlatformUserListItemVm> QueryPlatformUsers(
            string sql,
            PlatformUserListFilterVm filter,
            int skip,
            int take,
            int? tenantOrganizationId = null)
        {
            var items = new List<PlatformUserListItemVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    u.[Id],
    u.[EmployeeNumber],
    u.[FirstName],
    u.[LastName],
    u.[Email],
    u.[Phone],
    u.[DepartmentId],
    d.[Name] AS DepartmentName,
    u.[PositionTitle],
    u.[IsActive],
    u.[RoleId],
    r.[Name] AS RoleName,
    u.[OrganizationId],
    o.[Name] AS OrganizationName"
                        + sql;
                    AddPlatformFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@TenantOrganizationId", tenantOrganizationId.HasValue ? (object)tenantOrganizationId.Value : DBNull.Value);
                    if (sql.IndexOf("@Skip", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        SqlQueryHelper.AddParameter(command, "@Skip", skip);
                        SqlQueryHelper.AddParameter(command, "@Take", take);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapPlatformUser(reader));
                        }
                    }
                }
            }

            return items;
        }

        private void PaginatePlatformOrganizationGroups(
            string fromClause,
            string baseWhere,
            PlatformUserListFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            PlatformUserIndexViewModel viewModel)
        {
            var totalCount = viewModel.OrganizationCount;
            int safePage;
            var skip = ListPageHelper.ComputeSkip(page, pageSize, totalCount, out safePage);
            var orgSql = @"
SELECT org.[OrganizationId], org.[OrganizationName]
FROM (
    SELECT DISTINCT u.[OrganizationId], o.[Name] AS OrganizationName
" + fromClause + baseWhere + PlatformTenantPredicate + @"
) org
ORDER BY org.[OrganizationName], org.[OrganizationId]
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";

            var groups = new List<PlatformUserOrganizationGroupVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = orgSql;
                    AddPlatformFilterParameters(command, filter);
                    SqlQueryHelper.AddParameter(command, "@Skip", skip);
                    SqlQueryHelper.AddParameter(command, "@Take", pageSize);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var organizationId = Convert.ToInt32(reader["OrganizationId"]);
                            var organizationName = SqlQueryHelper.GetString(reader, "OrganizationName");
                            groups.Add(new PlatformUserOrganizationGroupVm
                            {
                                OrganizationId = organizationId,
                                OrganizationName = organizationName,
                                Users = QueryPlatformUsersForOrganization(fromClause, baseWhere, filter, organizationId, sort, direction)
                            });
                        }
                    }
                }
            }

            viewModel.OrganizationGroups = groups;
            ApplyActivePagination(viewModel, safePage, pageSize, totalCount);
        }

        private static void ApplyActivePagination(PlatformUserIndexViewModel viewModel, int safePage, int pageSize, int totalCount)
        {
            viewModel.Page = safePage;
            viewModel.ActiveTotalCount = totalCount;
            viewModel.ActiveTotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
            viewModel.ActiveStartItem = totalCount == 0 ? 0 : ((safePage - 1) * pageSize) + 1;
            viewModel.ActiveEndItem = Math.Min(safePage * pageSize, totalCount);
        }

        private static void AddPlatformFilterParameters(IDbCommand command, PlatformUserListFilterVm filter)
        {
            SqlQueryHelper.AddParameter(command, "@SearchPattern",
                filter == null || string.IsNullOrWhiteSpace(filter.Search)
                    ? DBNull.Value
                    : (object)SqlQueryHelper.BuildContainsPattern(filter.Search));
            SqlQueryHelper.AddParameter(command, "@OrganizationFilterId", filter?.OrganizationId.HasValue == true ? (object)filter.OrganizationId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@RoleId", filter?.RoleId.HasValue == true ? (object)filter.RoleId.Value : DBNull.Value);
            SqlQueryHelper.AddParameter(command, "@IsActive", filter?.IsActive.HasValue == true ? (object)(filter.IsActive.Value ? 1 : 0) : DBNull.Value);
        }

        private static UserVm MapUser(IDataRecord reader)
        {
            return new UserVm
            {
                Id = SqlQueryHelper.GetString(reader, "Id"),
                EmployeeNumber = SqlQueryHelper.GetString(reader, "EmployeeNumber"),
                FirstName = SqlQueryHelper.GetString(reader, "FirstName"),
                LastName = SqlQueryHelper.GetString(reader, "LastName"),
                Email = SqlQueryHelper.GetString(reader, "Email"),
                Phone = SqlQueryHelper.GetString(reader, "Phone"),
                DepartmentId = GetNullableInt(reader, "DepartmentId"),
                DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                PositionTitle = SqlQueryHelper.GetString(reader, "PositionTitle"),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                RoleId = GetNullableInt(reader, "RoleId"),
                RoleName = SqlQueryHelper.GetString(reader, "RoleName")
            };
        }

        private static PlatformUserListItemVm MapPlatformUser(IDataRecord reader)
        {
            return new PlatformUserListItemVm
            {
                Id = SqlQueryHelper.GetString(reader, "Id"),
                EmployeeNumber = SqlQueryHelper.GetString(reader, "EmployeeNumber"),
                FirstName = SqlQueryHelper.GetString(reader, "FirstName"),
                LastName = SqlQueryHelper.GetString(reader, "LastName"),
                Email = SqlQueryHelper.GetString(reader, "Email"),
                Phone = SqlQueryHelper.GetString(reader, "Phone"),
                DepartmentId = GetNullableInt(reader, "DepartmentId"),
                DepartmentName = SqlQueryHelper.GetString(reader, "DepartmentName"),
                PositionTitle = SqlQueryHelper.GetString(reader, "PositionTitle"),
                IsActive = Convert.ToBoolean(reader["IsActive"]),
                RoleId = GetNullableInt(reader, "RoleId"),
                RoleName = SqlQueryHelper.GetString(reader, "RoleName"),
                OrganizationId = GetNullableInt(reader, "OrganizationId"),
                OrganizationName = SqlQueryHelper.GetString(reader, "OrganizationName")
            };
        }

        private static int? GetNullableInt(IDataRecord record, string columnName)
        {
            var value = record[columnName];
            return value == DBNull.Value ? (int?)null : Convert.ToInt32(value);
        }
    }
}
