using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;
using NUnit.Framework;

namespace AssetManagement.PerformanceTests.Helpers
{
    public static class PerformanceTestSettings
    {
        public const int LargeDatasetMinimumAssets = 50000;
        public const int AssetListTimeoutMs = 200;
        public const int DashboardTimeoutMs = 500;
        public const int SearchTimeoutMs = 300;
        public const long MaxListWorkingSetBytes = 5L * 1024L * 1024L;

        public static string ConnectionString
        {
            get
            {
                var fromEnvironment = Environment.GetEnvironmentVariable("ASSETMANAGEMENT_TEST_CONNECTION");
                if (!string.IsNullOrWhiteSpace(fromEnvironment))
                {
                    return fromEnvironment;
                }

                var setting = ConfigurationManager.ConnectionStrings["AssetManagementConnection"];
                return setting == null ? null : setting.ConnectionString;
            }
        }

        public static bool TryGetDefaultOrganizationId(string connectionString, out int organizationId)
        {
            organizationId = 0;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return false;
            }

            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP 1 [Id] FROM [Organization] WHERE [Slug] = @Slug ORDER BY [Id]";
                    command.Parameters.Add(new SqlParameter("@Slug", "default"));
                    var result = command.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        command.Parameters.Clear();
                        command.CommandText = "SELECT TOP 1 [Id] FROM [Organization] ORDER BY [Id]";
                        result = command.ExecuteScalar();
                    }

                    if (result == null || result == DBNull.Value)
                    {
                        return false;
                    }

                    organizationId = Convert.ToInt32(result);
                    return true;
                }
            }
        }

        public static int CountActiveAssets(string connectionString, int organizationId)
        {
            using (var connection = new SqlConnection(connectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM [Asset] WHERE [OrganizationId] = @OrganizationId AND [IsActive] = 1";
                    command.Parameters.Add(new SqlParameter("@OrganizationId", organizationId));
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }
    }

    public sealed class FixedOrganizationScopeService : IOrganizationScopeService
    {
        private readonly int _organizationId;

        public FixedOrganizationScopeService(int organizationId)
        {
            _organizationId = organizationId;
        }

        public int? GetCurrentOrganizationId()
        {
            return _organizationId;
        }

        public int? GetTenantFilterOrganizationId(Type entityType)
        {
            return _organizationId;
        }

        public void SetOrganizationFilterOverride(int? organizationId)
        {
        }

        public bool IsImpersonating()
        {
            return false;
        }

        public bool IsPlatformAdmin()
        {
            return false;
        }

        public bool IsActualPlatformAdmin()
        {
            return false;
        }

        public bool IsCompanyAdmin()
        {
            return true;
        }

        public string GetImpersonationReason()
        {
            return null;
        }

        public IQueryable<T> ApplyOrganizationFilter<T>(IQueryable<T> query) where T : class
        {
            return query;
        }
    }

    public sealed class BypassDepartmentScopeService : IDepartmentScopeService
    {
        public bool BypassesDepartmentScope
        {
            get { return true; }
        }

        public int? ScopedDepartmentId
        {
            get { return null; }
        }

        public IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query)
        {
            return query;
        }

        public IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query)
        {
            return query;
        }

        public void EnsureCanAccessAsset(Asset asset)
        {
        }

        public void EnsureCanAccessDepartment(Department department)
        {
        }

        public void EnsureCanAccessDepartmentId(int departmentId)
        {
        }

        public int CountVisibleDepartments(bool activeOnly = true)
        {
            return 0;
        }
    }

    public sealed class StringSqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly string _connectionString;

        public StringSqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }

    public static class SqlTraceAssertions
    {
        private static readonly Regex FullAssetSelectPattern = new Regex(
            @"SELECT\s+\*\s+FROM\s+\[Asset\]",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static void AssertNoUnscopedFullAssetSelect(IEnumerable<string> executedCommands)
        {
            foreach (var command in executedCommands ?? Enumerable.Empty<string>())
            {
                if (FullAssetSelectPattern.IsMatch(command) &&
                    command.IndexOf("WHERE", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Assert.Fail("Detected unscoped full-table Asset SELECT: " + command);
                }
            }
        }

        public static void AssertAllAssetQueriesAreScoped(IEnumerable<string> executedCommands)
        {
            foreach (var command in executedCommands ?? Enumerable.Empty<string>())
            {
                if (command.IndexOf("FROM [Asset]", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    command.IndexOf("@OrganizationId", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    Assert.Fail("Asset query missing @OrganizationId predicate: " + command);
                }
            }
        }
    }

    public static class PerformanceMeasurement
    {
        public static long MeasureElapsedMilliseconds(Action action)
        {
            action();
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }

        public static long MeasureWorkingSetDelta(Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var before = Process.GetCurrentProcess().WorkingSet64;
            action();
            GC.Collect();
            var after = Process.GetCurrentProcess().WorkingSet64;
            return Math.Max(0, after - before);
        }
    }
}
