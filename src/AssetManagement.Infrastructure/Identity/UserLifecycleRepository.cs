using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Identity
{
    /// <summary>
    /// rel1009g user delete guard. Blocking references are discovered from the schema (every varchar/nvarchar column
    /// named *UserId, *ById, *UserIds, *CustodianId outside Users and backup tables), so new tables count automatically.
    /// Rows the user owns are removed with the user; LoginAttempts by e-mail count as sign-in history.
    /// </summary>
    public class UserLifecycleRepository : IUserLifecycleStore
    {
        private static readonly HashSet<string> OwnedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Notification", "IdentityUserClaims", "IdentityUserLogins", "IdentityUserRoles", "PasswordResetToken"
        };

        private const string CandidateColumnsSql = @"
SELECT c.TABLE_NAME, c.COLUMN_NAME, c.CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS c
INNER JOIN INFORMATION_SCHEMA.TABLES t ON t.TABLE_SCHEMA = c.TABLE_SCHEMA AND t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_TYPE = 'BASE TABLE'
WHERE c.TABLE_SCHEMA = 'dbo'
  AND c.DATA_TYPE IN ('nvarchar', 'varchar')
  AND (c.COLUMN_NAME LIKE '%UserId' OR c.COLUMN_NAME LIKE '%ById' OR c.COLUMN_NAME LIKE '%UserIds'
       OR c.COLUMN_NAME LIKE '%CustodianId' OR c.COLUMN_NAME = 'UserId' OR c.COLUMN_NAME = 'ActorUserId')
  AND c.TABLE_NAME <> 'Users'
  AND c.TABLE_NAME NOT LIKE 'Bak[_]%'
  AND c.TABLE_NAME NOT LIKE '[_]%'
ORDER BY c.TABLE_NAME, c.COLUMN_NAME";

        private const string CompanyAdminCountSql = @"
SELECT COUNT(1)
FROM [Users] u
INNER JOIN [Roles] r ON r.[Id] = u.[RoleId]
WHERE u.[OrganizationId] = @OrganizationId
  AND u.[IsActive] = 1
  AND " + AssetManagement.Application.Security.OrgCapabilities.TenantAdministratorRoleSqlPredicate + @"
  AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
  AND (@ExcludeUserId IS NULL OR u.[Id] <> @ExcludeUserId)";

        private const string IsCompanyAdminSql = @"
SELECT COUNT(1)
FROM [Users] u
INNER JOIN [Roles] r ON r.[Id] = u.[RoleId]
WHERE u.[Id] = @UserId AND u.[OrganizationId] = @OrganizationId
  AND " + AssetManagement.Application.Security.OrgCapabilities.TenantAdministratorRoleSqlPredicate;

        private readonly ISqlConnectionFactory _connectionFactory;

        public UserLifecycleRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<KeyValuePair<string, int>> GetDeleteBlockers(string userId, int organizationId)
        {
            var totals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                var columns = new List<Tuple<string, string, int>>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = CandidateColumnsSql;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            columns.Add(Tuple.Create(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader.GetValue(2))));
                        }
                    }
                }

                foreach (var column in columns)
                {
                    if (OwnedTables.Contains(column.Item1)
                        || (string.Equals(column.Item1, "UserInvitation", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(column.Item2, "UsedByUserId", StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    // CSV lists (…UserIds, wide columns) are matched by containment; ids are GUID strings.
                    var isList = column.Item2.EndsWith("UserIds", StringComparison.OrdinalIgnoreCase) || column.Item3 > 128 || column.Item3 == -1;
                    var sql = "SELECT COUNT(1) FROM [dbo].[" + column.Item1.Replace("]", "]]") + "] WHERE "
                        + (isList
                            ? "[" + column.Item2.Replace("]", "]]") + "] LIKE N'%' + @UserId + N'%'"
                            : "[" + column.Item2.Replace("]", "]]") + "] = @UserId");
                    var count = Convert.ToInt32(ExecuteScalar(connection, null, sql, new Dictionary<string, object> { { "@UserId", userId } }));
                    if (count > 0)
                    {
                        Add(totals, order, UserLifecycleRules.DescribeColumn(column.Item1, column.Item2), count);
                    }
                }

                var logins = Convert.ToInt32(ExecuteScalar(
                    connection,
                    null,
                    @"SELECT COUNT(1) FROM [LoginAttempts] la INNER JOIN [Users] u ON u.[Email] = la.[Username]
                      WHERE u.[Id] = @UserId AND (la.[OrganizationId] IS NULL OR la.[OrganizationId] = @OrganizationId)",
                    new Dictionary<string, object> { { "@UserId", userId }, { "@OrganizationId", organizationId } }));
                if (logins > 0)
                {
                    Add(totals, order, UserLifecycleRules.DescribeColumn("LoginAttempts", "Username"), logins);
                }
            }

            var result = new List<KeyValuePair<string, int>>();
            foreach (var key in order)
            {
                result.Add(new KeyValuePair<string, int>(key, totals[key]));
            }

            return result;
        }

        public int CountOtherActiveCompanyAdmins(int organizationId, string excludeUserId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                return Convert.ToInt32(ExecuteScalar(connection, null, CompanyAdminCountSql, new Dictionary<string, object>
                {
                    { "@OrganizationId", organizationId },
                    { "@ExcludeUserId", string.IsNullOrWhiteSpace(excludeUserId) ? null : excludeUserId }
                }));
            }
        }

        public bool IsCompanyAdmin(string userId, int organizationId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                return Convert.ToInt32(ExecuteScalar(connection, null, IsCompanyAdminSql, new Dictionary<string, object>
                {
                    { "@UserId", userId },
                    { "@OrganizationId", organizationId }
                })) > 0;
            }
        }

        public void HardDelete(string userId, int organizationId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
                {
                    var p = new Dictionary<string, object> { { "@UserId", userId }, { "@OrganizationId", organizationId } };
                    var exists = Convert.ToInt32(ExecuteScalar(connection, transaction,
                        "SELECT COUNT(1) FROM [Users] WITH (UPDLOCK) WHERE [Id] = @UserId AND [OrganizationId] = @OrganizationId", p));
                    if (exists == 0)
                    {
                        throw new BusinessException("User not found.");
                    }

                    foreach (var table in new[] { "Notification", "IdentityUserClaims", "IdentityUserLogins", "IdentityUserRoles", "PasswordResetToken" })
                    {
                        ExecuteNonQuery(connection, transaction,
                            "IF OBJECT_ID(N'dbo." + table + "', N'U') IS NOT NULL DELETE FROM [dbo].[" + table + "] WHERE [UserId] = @UserId", p);
                    }

                    ExecuteNonQuery(connection, transaction,
                        "IF COL_LENGTH(N'dbo.UserInvitation', N'UsedByUserId') IS NOT NULL UPDATE [UserInvitation] SET [UsedByUserId] = NULL WHERE [UsedByUserId] = @UserId", p);
                    var deleted = ExecuteNonQuery(connection, transaction,
                        "DELETE FROM [Users] WHERE [Id] = @UserId AND [OrganizationId] = @OrganizationId", p);
                    if (deleted != 1)
                    {
                        throw new BusinessException("User could not be deleted.");
                    }

                    transaction.Commit();
                }
            }
        }

        private static void Add(IDictionary<string, int> totals, IList<string> order, string key, int count)
        {
            if (!totals.ContainsKey(key))
            {
                totals[key] = 0;
                order.Add(key);
            }

            totals[key] += count;
        }

        private static object ExecuteScalar(IDbConnection connection, IDbTransaction transaction, string sql, IDictionary<string, object> parameters)
        {
            using (var command = Build(connection, transaction, sql, parameters))
            {
                return command.ExecuteScalar();
            }
        }

        private static int ExecuteNonQuery(IDbConnection connection, IDbTransaction transaction, string sql, IDictionary<string, object> parameters)
        {
            using (var command = Build(connection, transaction, sql, parameters))
            {
                return command.ExecuteNonQuery();
            }
        }

        private static IDbCommand Build(IDbConnection connection, IDbTransaction transaction, string sql, IDictionary<string, object> parameters)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;
            if (parameters != null)
            {
                foreach (var pair in parameters)
                {
                    var parameter = command.CreateParameter();
                    parameter.ParameterName = pair.Key;
                    parameter.Value = pair.Value ?? DBNull.Value;
                    command.Parameters.Add(parameter);
                }
            }

            return command;
        }
    }
}
