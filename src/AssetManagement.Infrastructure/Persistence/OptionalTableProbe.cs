using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;

namespace AssetManagement.Infrastructure.Persistence
{
    /// <summary>
    /// For tables added by a migration that may not be applied yet on a given database (e.g. AssetMovement / 093).
    /// "Exists" is cached for the app domain; "missing" is re-checked after a short interval so the
    /// table is picked up as soon as the migration runs, without a recycle.
    /// </summary>
    public static class OptionalTableProbe
    {
        private static readonly TimeSpan MissingRecheckInterval = TimeSpan.FromSeconds(60);
        private static readonly ConcurrentDictionary<string, DateTime> MissingSinceUtc = new ConcurrentDictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, bool> Present = new ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        public static bool Exists(SqlConnection connection, SqlTransaction transaction, string tableName)
        {
            var key = connection.Database + "|" + tableName;
            if (Present.ContainsKey(key))
            {
                return true;
            }

            DateTime checkedAt;
            if (MissingSinceUtc.TryGetValue(key, out checkedAt) && DateTime.UtcNow - checkedAt < MissingRecheckInterval)
            {
                return false;
            }

            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(@Name, N'U') IS NULL THEN 0 ELSE 1 END";
                command.Parameters.AddWithValue("@Name", "[dbo].[" + tableName + "]");
                var exists = Convert.ToInt32(command.ExecuteScalar()) == 1;
                if (exists)
                {
                    Present[key] = true;
                    DateTime ignored;
                    MissingSinceUtc.TryRemove(key, out ignored);
                }
                else
                {
                    MissingSinceUtc[key] = DateTime.UtcNow;
                    System.Diagnostics.Trace.TraceWarning("Table {0} is missing (migration not applied); its writes are skipped.", tableName);
                }

                return exists;
            }
        }

        /// <summary>True when the exception is SQL "Invalid object name" (error 208) for a missing table.</summary>
        public static bool IsMissingTable(Exception ex)
        {
            var sql = ex as SqlException;
            return sql != null && sql.Number == 208;
        }
    }
}
