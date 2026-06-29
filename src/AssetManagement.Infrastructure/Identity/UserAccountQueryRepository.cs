using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts.Queries;
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
