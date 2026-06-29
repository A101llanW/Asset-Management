using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
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
                ApplyScripts(connectionString, ResolveScriptsRoot(), null);
                _initialized = true;
            }
        }

        /// <summary>Applies only database/scripts/004_Migrations (idempotent ALTER scripts).</summary>
        public static void ApplyMigrations(string connectionStringName, bool continueOnError = true)
        {
            lock (SyncRoot)
            {
                var connectionString = ConfigurationManager.ConnectionStrings[connectionStringName].ConnectionString;
                EnsureDatabaseExists(connectionString);
                var migrationsRoot = Path.Combine(ResolveScriptsRoot(), "004_Migrations");
                if (!Directory.Exists(migrationsRoot))
                {
                    throw new InvalidOperationException("Migrations folder not found: " + migrationsRoot);
                }

                ApplyScripts(connectionString, migrationsRoot, continueOnError);
            }
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

        private static void ApplyScripts(string connectionString, string scriptsRoot, bool? continueOnError)
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
                foreach (var scriptFile in scriptFiles)
                {
                    try
                    {
                        ExecuteScriptFile(connection, scriptFile);
                        if (continueOnError.HasValue)
                        {
                            Console.WriteLine("  OK  " + Path.GetFileName(scriptFile));
                        }
                    }
                    catch (SqlException ex) when (continueOnError == true)
                    {
                        Console.WriteLine("  SKIP " + Path.GetFileName(scriptFile) + ": " + ex.Message);
                    }
                }
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
