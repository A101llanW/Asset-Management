using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Caching;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    public class ReferenceDataCache : IReferenceDataCache
    {
        private static readonly TimeSpan DepartmentTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan RoleTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan SettingsTtl = TimeSpan.FromMinutes(10);

        private const string DepartmentsSql = @"
SELECT [Id], [Name], [Code], [Description], [IsActive]
FROM [Department]
WHERE [OrganizationId] = @OrganizationId
  AND (@ActiveOnly = 0 OR [IsActive] = 1)
ORDER BY [Name]";

        private const string RolesSql = @"
SELECT [Id], [Name], [Description], [IsSystemRole], [IsActive]
FROM [Roles]
WHERE [OrganizationId] = @OrganizationId
  AND [IsActive] = 1
ORDER BY [Name]";

        private const string CategoriesSql = @"
SELECT [Id], [Name], [IsActive]
FROM [AssetCategory]
WHERE [OrganizationId] = @OrganizationId
  AND (@ActiveOnly = 0 OR [IsActive] = 1)
ORDER BY [Name]";

        private const string AssetTypesSql = @"
SELECT [Id], [Name], [AssetCategoryId], [IsActive]
FROM [AssetType]
WHERE [OrganizationId] = @OrganizationId
  AND (@ActiveOnly = 0 OR [IsActive] = 1)
ORDER BY [Name]";

        private const string SuppliersSql = @"
SELECT [Id], [SupplierName], [ContactPerson], [Email], [Phone], [Address], [RegistrationNumber], [Notes], [IsActive]
FROM [Supplier]
WHERE [OrganizationId] = @OrganizationId
  AND (@ActiveOnly = 0 OR [IsActive] = 1)
ORDER BY [SupplierName]";

        private const string SettingsSql = @"
SELECT [SettingKey], [SettingValue]
FROM [SystemSetting]
WHERE [OrganizationId] = @OrganizationId
  AND [IsActive] = 1";

        private const string UsersForDropdownSql = @"
SELECT TOP 500
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
    u.[RoleId]
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
WHERE u.[OrganizationId] = @OrganizationId
  AND u.[IsActive] = 1
  AND (@DepartmentId IS NULL OR u.[DepartmentId] = @DepartmentId)
ORDER BY u.[LastName], u.[FirstName], u.[Email]";

        private const string UsersByIdsSqlTemplate = @"
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
    u.[RoleId]
FROM [Users] u
LEFT JOIN [Department] d ON d.[Id] = u.[DepartmentId]
WHERE u.[OrganizationId] = @OrganizationId
  AND u.[Id] IN ({0})";

        private readonly ISqlConnectionFactory _connectionFactory;

        public ReferenceDataCache(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<DepartmentVm> GetDepartments(int organizationId, bool activeOnly = true)
        {
            var cacheKey = "org:" + organizationId + ":depts:" + (activeOnly ? "active" : "all");
            var cached = GetCached<IList<DepartmentVm>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var items = LoadDepartments(organizationId, activeOnly);
            SetCached(cacheKey, items, DepartmentTtl);
            return items;
        }

        public IList<RoleVm> GetRoles(int organizationId)
        {
            var cacheKey = "org:" + organizationId + ":roles";
            var cached = GetCached<IList<RoleVm>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var items = LoadRoles(organizationId);
            SetCached(cacheKey, items, RoleTtl);
            return items;
        }

        public IDictionary<string, string> GetSettings(int organizationId)
        {
            var cacheKey = "org:" + organizationId + ":settings";
            var cached = GetCached<IDictionary<string, string>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var items = LoadSettings(organizationId);
            SetCached(cacheKey, items, SettingsTtl);
            return items;
        }

        public IList<UserVm> GetUsersForDropdown(int organizationId, int? departmentId = null)
        {
            var cacheKey = "org:" + organizationId + ":users:" + (departmentId.HasValue ? departmentId.Value.ToString() : "all");
            var cached = GetCached<IList<UserVm>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var items = LoadUsersForDropdown(organizationId, departmentId);
            SetCached(cacheKey, items, DepartmentTtl);
            return items;
        }

        public IList<UserVm> GetUsersByIds(int organizationId, IEnumerable<string> userIds)
        {
            var ids = userIds == null
                ? new List<string>()
                : userIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (ids.Count == 0)
            {
                return new List<UserVm>();
            }

            return LoadUsersByIds(organizationId, ids);
        }

        public IList<CategoryLookupVm> GetCategories(int organizationId, bool activeOnly = true)
        {
            var cacheKey = "org:" + organizationId + ":categories:" + (activeOnly ? "active" : "all");
            var cached = GetCached<IList<CategoryLookupVm>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var items = LoadCategories(organizationId, activeOnly);
            SetCached(cacheKey, items, DepartmentTtl);
            return items;
        }

        public IList<AssetTypeLookupVm> GetAssetTypes(int organizationId, bool activeOnly = true)
        {
            var cacheKey = "org:" + organizationId + ":assettypes:" + (activeOnly ? "active" : "all");
            var cached = GetCached<IList<AssetTypeLookupVm>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var items = LoadAssetTypes(organizationId, activeOnly);
            SetCached(cacheKey, items, DepartmentTtl);
            return items;
        }

        public IList<SupplierVm> GetSuppliers(int organizationId, bool activeOnly = true)
        {
            var cacheKey = "org:" + organizationId + ":suppliers:" + (activeOnly ? "active" : "all");
            var cached = GetCached<IList<SupplierVm>>(cacheKey);
            if (cached != null)
            {
                return cached;
            }

            var items = LoadSuppliers(organizationId, activeOnly);
            SetCached(cacheKey, items, DepartmentTtl);
            return items;
        }

        public void InvalidateDepartments(int organizationId)
        {
            RemoveCacheKey("org:" + organizationId + ":depts:active");
            RemoveCacheKey("org:" + organizationId + ":depts:all");
        }

        public void InvalidateRoles(int organizationId)
        {
            RemoveCacheKey("org:" + organizationId + ":roles");
        }

        public void InvalidateSettings(int organizationId)
        {
            RemoveCacheKey("org:" + organizationId + ":settings");
        }

        private IList<DepartmentVm> LoadDepartments(int organizationId, bool activeOnly)
        {
            var items = new List<DepartmentVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = DepartmentsSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@ActiveOnly", activeOnly ? 1 : 0);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new DepartmentVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Name = SqlQueryHelper.GetString(reader, "Name"),
                                Code = SqlQueryHelper.GetString(reader, "Code"),
                                Description = SqlQueryHelper.GetString(reader, "Description"),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        private IList<RoleVm> LoadRoles(int organizationId)
        {
            var items = new List<RoleVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = RolesSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);

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

        private IDictionary<string, string> LoadSettings(int organizationId)
        {
            var items = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = SettingsSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var key = SqlQueryHelper.GetString(reader, "SettingKey");
                            if (!string.IsNullOrWhiteSpace(key))
                            {
                                items[key] = SqlQueryHelper.GetString(reader, "SettingValue");
                            }
                        }
                    }
                }
            }

            return items;
        }

        private IList<UserVm> LoadUsersForDropdown(int organizationId, int? departmentId)
        {
            var items = new List<UserVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = UsersForDropdownSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@DepartmentId", departmentId.HasValue ? (object)departmentId.Value : DBNull.Value);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapUser(reader));
                        }
                    }
                }
            }

            return items;
        }

        private IList<UserVm> LoadUsersByIds(int organizationId, IList<string> userIds)
        {
            var parameterNames = new List<string>();
            for (var i = 0; i < userIds.Count; i++)
            {
                parameterNames.Add("@UserId" + i);
            }

            var sql = string.Format(UsersByIdsSqlTemplate, string.Join(", ", parameterNames.ToArray()));
            var items = new List<UserVm>();

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    for (var i = 0; i < userIds.Count; i++)
                    {
                        SqlQueryHelper.AddParameter(command, parameterNames[i], userIds[i]);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(MapUser(reader));
                        }
                    }
                }
            }

            return items;
        }

        private IList<CategoryLookupVm> LoadCategories(int organizationId, bool activeOnly)
        {
            var items = new List<CategoryLookupVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = CategoriesSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@ActiveOnly", activeOnly ? 1 : 0);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new CategoryLookupVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Name = SqlQueryHelper.GetString(reader, "Name"),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        private IList<AssetTypeLookupVm> LoadAssetTypes(int organizationId, bool activeOnly)
        {
            var items = new List<AssetTypeLookupVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = AssetTypesSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@ActiveOnly", activeOnly ? 1 : 0);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new AssetTypeLookupVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                Name = SqlQueryHelper.GetString(reader, "Name"),
                                AssetCategoryId = Convert.ToInt32(reader["AssetCategoryId"]),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        private IList<SupplierVm> LoadSuppliers(int organizationId, bool activeOnly)
        {
            var items = new List<SupplierVm>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = SuppliersSql;
                    SqlQueryHelper.AddParameter(command, "@OrganizationId", organizationId);
                    SqlQueryHelper.AddParameter(command, "@ActiveOnly", activeOnly ? 1 : 0);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            items.Add(new SupplierVm
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                SupplierName = SqlQueryHelper.GetString(reader, "SupplierName"),
                                ContactPerson = SqlQueryHelper.GetString(reader, "ContactPerson"),
                                Email = SqlQueryHelper.GetString(reader, "Email"),
                                Phone = SqlQueryHelper.GetString(reader, "Phone"),
                                Address = SqlQueryHelper.GetString(reader, "Address"),
                                RegistrationNumber = SqlQueryHelper.GetString(reader, "RegistrationNumber"),
                                Notes = SqlQueryHelper.GetString(reader, "Notes"),
                                IsActive = Convert.ToBoolean(reader["IsActive"])
                            });
                        }
                    }
                }
            }

            return items;
        }

        private static UserVm MapUser(IDataRecord record)
        {
            return new UserVm
            {
                Id = SqlQueryHelper.GetString(record, "Id"),
                EmployeeNumber = SqlQueryHelper.GetString(record, "EmployeeNumber"),
                FirstName = SqlQueryHelper.GetString(record, "FirstName"),
                LastName = SqlQueryHelper.GetString(record, "LastName"),
                Email = SqlQueryHelper.GetString(record, "Email"),
                Phone = SqlQueryHelper.GetString(record, "Phone"),
                DepartmentId = record["DepartmentId"] == DBNull.Value ? (int?)null : Convert.ToInt32(record["DepartmentId"]),
                DepartmentName = SqlQueryHelper.GetString(record, "DepartmentName"),
                PositionTitle = SqlQueryHelper.GetString(record, "PositionTitle"),
                IsActive = Convert.ToBoolean(record["IsActive"]),
                RoleId = record["RoleId"] == DBNull.Value ? (int?)null : Convert.ToInt32(record["RoleId"])
            };
        }

        private static T GetCached<T>(string cacheKey) where T : class
        {
            if (HttpRuntime.Cache == null)
            {
                return null;
            }

            return HttpRuntime.Cache[cacheKey] as T;
        }

        private static void SetCached(string cacheKey, object value, TimeSpan ttl)
        {
            if (HttpRuntime.Cache == null || value == null)
            {
                return;
            }

            HttpRuntime.Cache.Insert(
                cacheKey,
                value,
                null,
                Cache.NoAbsoluteExpiration,
                ttl,
                CacheItemPriority.Default,
                null);
        }

        private static void RemoveCacheKey(string cacheKey)
        {
            if (HttpRuntime.Cache == null)
            {
                return;
            }

            HttpRuntime.Cache.Remove(cacheKey);
        }
    }
}
