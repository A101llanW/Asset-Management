using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AssetManagement.Infrastructure.Persistence
{
    public static class SqlDatabaseInitializer
    {
        private static readonly object SyncRoot = new object();
        private static bool _initialized;

        public static void Initialize(string connectionStringName)
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                var connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
                EnsureDatabaseExists(connectionString);
                ApplyScripts(connectionString, ResolveScriptsRoot(), null, trackMigrationHistory: false);
                _initialized = true;
            }
        }

        /// <summary>Applies only database/scripts/004_Migrations (idempotent ALTER scripts).</summary>
        public static void ApplyMigrations(string connectionStringName, bool continueOnError = true)
        {
            var setting = ConfigurationManager.ConnectionStrings[connectionStringName];
            if (setting == null || string.IsNullOrWhiteSpace(setting.ConnectionString))
            {
                throw new InvalidOperationException("Connection string not found: " + connectionStringName);
            }

            ApplyMigrationsToConnection(setting.ConnectionString, continueOnError);
        }

        /// <summary>Applies 004_Migrations to an explicit connection string (e.g. test runners).</summary>
        public static void ApplyMigrationsToConnection(string connectionString, bool continueOnError = true)
        {
            lock (SyncRoot)
            {
                EnsureDatabaseExists(connectionString);
                var migrationsRoot = Path.Combine(ResolveScriptsRoot(), "004_Migrations");
                if (!Directory.Exists(migrationsRoot))
                {
                    throw new InvalidOperationException("Migrations folder not found: " + migrationsRoot);
                }

                ApplyScripts(connectionString, migrationsRoot, continueOnError, trackMigrationHistory: true);
            }
        }

        public static bool ResolveMigrationContinueOnError()
        {
            var setting = ConfigurationManager.AppSettings["MigrationContinueOnError"];
            if (string.IsNullOrWhiteSpace(setting))
            {
                return true;
            }

            return !setting.Equals("false", StringComparison.OrdinalIgnoreCase);
        }

        internal static void ResetForTesting()
        {
            lock (SyncRoot)
            {
                _initialized = false;
            }
        }

        private static void EnsureDatabaseExists(string connectionString)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var databaseName = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(databaseName))
            {
                throw new InvalidOperationException("Connection string must specify Initial Catalog.");
            }

            builder.InitialCatalog = "master";
            EnsureConnectionTimeout(builder);

            using (var connection = OpenConnectionWithRetry(builder.ConnectionString))
            {
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "IF DB_ID(@databaseName) IS NULL BEGIN EXEC('CREATE DATABASE [' + @databaseName + ']'); END";
                    command.Parameters.AddWithValue("@databaseName", databaseName);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void ApplyScripts(string connectionString, string scriptsRoot, bool? continueOnError, bool trackMigrationHistory)
        {
            var largeDatasetSuffix = Path.DirectorySeparatorChar + "002_Seed" + Path.DirectorySeparatorChar + "003_LargeDataset.sql";
            List<string> scriptFiles;
            if (continueOnError.HasValue)
            {
                scriptFiles = Directory.GetFiles(scriptsRoot, "*.sql", SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                scriptFiles = Directory.GetFiles(scriptsRoot, "*.sql", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith(largeDatasetSuffix, StringComparison.OrdinalIgnoreCase)
                        && !path.Replace('/', '\\').EndsWith(largeDatasetSuffix, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => GetScriptSortKey(path))
                    .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            if (scriptFiles.Count == 0)
            {
                throw new InvalidOperationException("No SQL scripts found in " + scriptsRoot + ".");
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            EnsureConnectionTimeout(builder);

            using (var connection = OpenConnectionWithRetry(builder.ConnectionString))
            {
                if (trackMigrationHistory)
                {
                    EnsureMigrationHistoryTable(connection);
                    BootstrapLegacyMigrationHistory(connection, scriptFiles);
                    RepairFalsePositiveMigrationHistory(connection);
                }

                foreach (var scriptFile in scriptFiles)
                {
                    var scriptName = Path.GetFileName(scriptFile);
                    if (trackMigrationHistory && IsMigrationApplied(connection, scriptName))
                    {
                        Console.WriteLine("  SKIP (applied) " + scriptName);
                        continue;
                    }

                    try
                    {
                        ExecuteScriptFile(connection, scriptFile);
                        if (trackMigrationHistory)
                        {
                            RecordMigrationApplied(connection, scriptFile, scriptName);
                        }

                        if (continueOnError.HasValue)
                        {
                            Console.WriteLine("  OK  " + scriptName);
                        }
                    }
                    catch (SqlException ex) when (continueOnError == true)
                    {
                        Console.WriteLine("  SKIP " + scriptName + ": " + ex.Message);
                    }
                }
            }
        }

        private const string MigrationHistoryTable = "SchemaMigrationHistory";
        private const int LegacyBootstrapMaxMigrationNumber = 58;

        private static void EnsureMigrationHistoryTable(SqlConnection connection)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
IF OBJECT_ID(N'[dbo].[SchemaMigrationHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SchemaMigrationHistory] (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [ScriptName] NVARCHAR(260) NOT NULL,
        [Checksum] NVARCHAR(64) NULL,
        [AppliedAt] DATETIME2 NOT NULL CONSTRAINT [DF_SchemaMigrationHistory_AppliedAt] DEFAULT (SYSUTCDATETIME()),
        [AppliedBy] NVARCHAR(128) NOT NULL CONSTRAINT [DF_SchemaMigrationHistory_AppliedBy] DEFAULT (SUSER_SNAME()),
        CONSTRAINT [UQ_SchemaMigrationHistory_ScriptName] UNIQUE ([ScriptName])
    );
END";
                command.ExecuteNonQuery();
            }
        }

        private static void BootstrapLegacyMigrationHistory(SqlConnection connection, IList<string> scriptFiles)
        {
            if (GetAppliedMigrationCount(connection) > 0)
            {
                return;
            }

            if (!TableExists(connection, "Organization"))
            {
                return;
            }

            var legacyCount = 0;
            foreach (var scriptFile in scriptFiles)
            {
                var scriptName = Path.GetFileName(scriptFile);
                int migrationNumber;
                if (!TryGetMigrationNumber(scriptName, out migrationNumber)
                    || migrationNumber > LegacyBootstrapMaxMigrationNumber)
                {
                    continue;
                }

                RecordMigrationApplied(connection, scriptFile, scriptName, "legacy-bootstrap");
                legacyCount++;
            }

            Console.WriteLine("  INFO legacy database detected; recorded " + legacyCount + " migration(s) without re-running.");
        }

        private static void RepairFalsePositiveMigrationHistory(SqlConnection connection)
        {
            RepairIfSchemaMissing(
                connection,
                "059_DepartmentHierarchy.sql",
                () => ColumnExists(connection, "Department", "ParentDepartmentId")
                    && ColumnExists(connection, "Department", "DepartmentKind")
                    && ColumnExists(connection, "Department", "IsRequisitionTarget"));

            RepairIfSchemaMissing(
                connection,
                "060_SchoolRolesAndPermissions.sql",
                () => PermissionExists(connection, "Purchases.CreateForAnyDepartment"));
        }

        private static void RepairIfSchemaMissing(SqlConnection connection, string scriptName, Func<bool> schemaSatisfied)
        {
            if (!IsMigrationApplied(connection, scriptName) || schemaSatisfied())
            {
                return;
            }

            DeleteMigrationHistoryEntry(connection, scriptName);
            Console.WriteLine("  REPAIR removed false-positive history for " + scriptName);
        }

        private static void DeleteMigrationHistoryEntry(SqlConnection connection, string scriptName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "DELETE FROM [SchemaMigrationHistory] WHERE [ScriptName]=@ScriptName";
                command.Parameters.AddWithValue("@ScriptName", scriptName);
                command.ExecuteNonQuery();
            }
        }

        private static bool TryGetMigrationNumber(string scriptName, out int migrationNumber)
        {
            migrationNumber = 0;
            if (string.IsNullOrWhiteSpace(scriptName))
            {
                return false;
            }

            var underscoreIndex = scriptName.IndexOf('_');
            if (underscoreIndex <= 0)
            {
                return false;
            }

            return int.TryParse(scriptName.Substring(0, underscoreIndex), out migrationNumber);
        }

        private static bool ColumnExists(SqlConnection connection, string tableName, string columnName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CASE WHEN COL_LENGTH(@TableName, @ColumnName) IS NULL THEN 0 ELSE 1 END";
                command.Parameters.AddWithValue("@TableName", tableName);
                command.Parameters.AddWithValue("@ColumnName", columnName);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        private static bool PermissionExists(SqlConnection connection, string permissionCode)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM [Permission] WHERE [Code]=@Code";
                command.Parameters.AddWithValue("@Code", permissionCode);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static bool TableExists(SqlConnection connection, string tableName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(@tableName, 'U') IS NULL THEN 0 ELSE 1 END";
                command.Parameters.AddWithValue("@tableName", "dbo." + tableName);
                return Convert.ToInt32(command.ExecuteScalar()) == 1;
            }
        }

        private static int GetAppliedMigrationCount(SqlConnection connection)
        {
            if (!TableExists(connection, MigrationHistoryTable))
            {
                return 0;
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM [SchemaMigrationHistory]";
                return Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private static bool IsMigrationApplied(SqlConnection connection, string scriptName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(1) FROM [SchemaMigrationHistory] WHERE [ScriptName]=@ScriptName";
                command.Parameters.AddWithValue("@ScriptName", scriptName);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        private static void RecordMigrationApplied(SqlConnection connection, string scriptFile, string scriptName, string appliedByOverride = null)
        {
            var checksum = ComputeFileChecksum(scriptFile);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
IF NOT EXISTS (SELECT 1 FROM [SchemaMigrationHistory] WHERE [ScriptName]=@ScriptName)
BEGIN
    INSERT INTO [SchemaMigrationHistory] ([ScriptName], [Checksum], [AppliedBy])
    VALUES (@ScriptName, @Checksum, @AppliedBy);
END";
                command.Parameters.AddWithValue("@ScriptName", scriptName);
                command.Parameters.AddWithValue("@Checksum", (object)checksum ?? DBNull.Value);
                command.Parameters.AddWithValue("@AppliedBy", appliedByOverride ?? Environment.UserName);
                command.ExecuteNonQuery();
            }
        }

        private static string ComputeFileChecksum(string scriptFile)
        {
            if (string.IsNullOrWhiteSpace(scriptFile) || !File.Exists(scriptFile))
            {
                return null;
            }

            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(scriptFile))
            {
                var hash = sha.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static int GetScriptSortKey(string path)
        {
            var normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/001_Schema/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 100;
            }

            if (normalized.IndexOf("/004_Migrations/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 200;
            }

            if (normalized.IndexOf("/002_Seed/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 300;
            }

            if (normalized.IndexOf("/003_Indexes/", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 400;
            }

            return 500;
        }

        private static void EnsureConnectionTimeout(SqlConnectionStringBuilder builder)
        {
            if (builder.ConnectTimeout < 60)
            {
                builder.ConnectTimeout = 60;
            }
        }

        private static SqlConnection OpenConnectionWithRetry(string connectionString)
        {
            const int maxAttempts = 3;
            Exception lastError = null;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var connection = new SqlConnection(connectionString);
                try
                {
                    connection.Open();
                    return connection;
                }
                catch (SqlException ex)
                {
                    lastError = ex;
                    connection.Dispose();

                    if (attempt == maxAttempts || !IsTransientConnectionError(ex))
                    {
                        throw BuildConnectionException(connectionString, ex);
                    }

                    System.Threading.Thread.Sleep(2000 * attempt);
                }
            }

            throw BuildConnectionException(connectionString, lastError);
        }

        private static bool IsTransientConnectionError(SqlException exception)
        {
            if (exception == null)
            {
                return false;
            }

            foreach (SqlError error in exception.Errors)
            {
                // -2 timeout, 53/121 network/login, 233 pipe, 10054/10060 network
                if (error.Number == -2
                    || error.Number == 53
                    || error.Number == 121
                    || error.Number == 233
                    || error.Number == 10054
                    || error.Number == 10060)
                {
                    return true;
                }
            }

            return false;
        }

        private static InvalidOperationException BuildConnectionException(string connectionString, Exception innerException)
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var message =
                "Unable to connect to SQL Server at '" + builder.DataSource
                + "'. Verify the instance is running, the connection string in Web.config is correct, "
                + "and that the application identity can access the database. "
                + "For LocalDB, prefer SQL Express (Data Source=.\\SQLEXPRESS) when hosting under IIS.";

            return new InvalidOperationException(message, innerException);
        }

        private static void ExecuteScriptFile(SqlConnection connection, string scriptFile)
        {
            var script = File.ReadAllText(scriptFile);
            foreach (var batch in SplitBatches(script))
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = batch;
                    command.CommandTimeout = 120;
                    command.ExecuteNonQuery();
                }
            }
        }

        internal static IEnumerable<string> SplitBatches(string script)
        {
            var batches = new List<string>();
            var batch = new StringBuilder();
            var lines = script.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
                {
                    if (batch.Length > 0)
                    {
                        batches.Add(batch.ToString());
                        batch.Clear();
                    }

                    continue;
                }

                batch.AppendLine(line);
            }

            if (batch.Length > 0)
            {
                batches.Add(batch.ToString());
            }

            return batches;
        }

        internal static string ResolveScriptsRoot()
        {
            var configuredPath = ConfigurationManager.AppSettings["DatabaseScriptsPath"];
            if (!string.IsNullOrWhiteSpace(configuredPath))
            {
                var fullPath = Path.GetFullPath(configuredPath);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }

                throw new InvalidOperationException("DatabaseScriptsPath does not exist: " + fullPath);
            }

            var current = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (current != null)
            {
                var candidate = Path.Combine(current.FullName, "database", "scripts");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate database/scripts. Set appSettings DatabaseScriptsPath or deploy scripts alongside the application.");
        }
    }
}
