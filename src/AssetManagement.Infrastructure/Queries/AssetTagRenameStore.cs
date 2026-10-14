using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>rel1013b: SQL side of the asset tag rename tool (tables from migration 103).</summary>
    public class AssetTagRenameStore : IAssetTagRenameStore
    {
        private const int MaxConflictsReported = 10;
        private readonly ISqlConnectionFactory _connectionFactory;

        public AssetTagRenameStore(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<AssetTagRenameCandidate> GetCandidates(int organizationId, ICollection<int> departmentIds, bool includeUnassigned)
        {
            var result = new List<AssetTagRenameCandidate>();
            var ids = (departmentIds ?? new int[0]).Distinct().ToList();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                var where = new List<string>();
                if (ids.Count > 0)
                {
                    var names = new List<string>();
                    for (var i = 0; i < ids.Count; i++)
                    {
                        names.Add("@d" + i);
                        command.Parameters.Add("@d" + i, SqlDbType.Int).Value = ids[i];
                    }

                    where.Add("a.[DepartmentId] IN (" + string.Join(",", names) + ")");
                }

                if (includeUnassigned)
                {
                    where.Add("a.[DepartmentId] IS NULL");
                }

                if (departmentIds != null && where.Count == 0)
                {
                    return result;
                }

                command.CommandText = @"
SELECT a.[Id], a.[AssetTag], a.[AssetName], a.[DepartmentId]
FROM [dbo].[Asset] a
WHERE a.[OrganizationId] = @Org AND a.[IsActive] = 1 AND a.[AssetTag] IS NOT NULL AND LTRIM(RTRIM(a.[AssetTag])) <> N''"
                    + (departmentIds == null && !includeUnassigned ? " AND a.[DepartmentId] IS NOT NULL" : string.Empty)
                    + (where.Count > 0 ? " AND (" + string.Join(" OR ", where) + ")" : string.Empty)
                    + " ORDER BY a.[Id]";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new AssetTagRenameCandidate
                        {
                            AssetId = reader.GetInt32(0),
                            AssetTag = reader.IsDBNull(1) ? null : reader.GetString(1),
                            AssetName = reader.IsDBNull(2) ? null : reader.GetString(2),
                            DepartmentId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)
                        });
                    }
                }
            }

            return result;
        }

        public ISet<string> GetTakenTags(int organizationId, string prefix)
        {
            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT [AssetTag] FROM [dbo].[Asset] WHERE [OrganizationId] = @Org AND [AssetTag] LIKE @Like
UNION SELECT [AliasTag] FROM [dbo].[AssetTagAlias] WHERE [OrganizationId] = @Org AND [IsActive] = 1 AND [AliasTag] LIKE @Like";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Like", SqlDbType.NVarChar, 80).Value = EscapeLike(prefix) + "-%";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!reader.IsDBNull(0))
                        {
                            taken.Add(reader.GetString(0));
                        }
                    }
                }
            }

            return taken;
        }

        public int PeekNext(int organizationId, string prefix)
        {
            return new AssetTagSequenceStore(_connectionFactory).PeekNext(organizationId, prefix);
        }

        public bool AliasExists(int organizationId, string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                return false;
            }

            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT COUNT(*) FROM [dbo].[AssetTagAlias] WHERE [OrganizationId] = @Org AND [IsActive] = 1 AND [AliasTag] = @Tag";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Tag", SqlDbType.NVarChar, 60).Value = tag.Trim();
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }

        public AssetTagRenameOutcome Apply(int organizationId, string actorUserId, int? scopeDepartmentId, string scopeLabel, IList<AssetTagRenamePlanItem> items)
        {
            var outcome = new AssetTagRenameOutcome();
            if (items == null || items.Count == 0)
            {
                outcome.Message = "Nothing to rename.";
                return outcome;
            }

            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    AcquireTenantLock(connection, tx, organizationId);
                    var batchId = Convert.ToInt32(Scalar(connection, tx,
                        "INSERT INTO [dbo].[AssetTagRenameBatch] ([OrganizationId],[ScopeDepartmentId],[ScopeLabel],[AssetCount],[Status],[AppliedById],[AppliedAt]) OUTPUT INSERTED.[Id] VALUES (@Org,@Scope,@Label,0,N'Applied',@Actor,SYSUTCDATETIME())",
                        P("@Org", SqlDbType.Int, organizationId),
                        P("@Scope", SqlDbType.Int, scopeDepartmentId.HasValue ? (object)scopeDepartmentId.Value : DBNull.Value),
                        P("@Label", SqlDbType.NVarChar, Truncate(scopeLabel ?? "Tenant", 400), 400),
                        P("@Actor", SqlDbType.NVarChar, (object)actorUserId ?? DBNull.Value, 128)));

                    var nextByPrefix = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var conflicts = new List<string>();
                    foreach (var item in items)
                    {
                        var current = Scalar(connection, tx,
                            "SELECT [AssetTag] FROM [dbo].[Asset] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = @Id AND [OrganizationId] = @Org AND [IsActive] = 1",
                            P("@Id", SqlDbType.Int, item.AssetId), P("@Org", SqlDbType.Int, organizationId)) as string;
                        if (!string.Equals(current, item.OldTag, StringComparison.Ordinal))
                        {
                            conflicts.Add("asset " + item.AssetId + ": tag is now '" + (current ?? "(none)") + "', preview had '" + item.OldTag + "'");
                            continue;
                        }

                        int next;
                        if (!nextByPrefix.TryGetValue(item.Prefix, out next))
                        {
                            next = LockAndReadNext(connection, tx, organizationId, item.Prefix);
                        }

                        string newTag;
                        while (true)
                        {
                            newTag = item.Prefix + "-" + next.ToString("000");
                            if (!TagTaken(connection, tx, organizationId, newTag, null, null))
                            {
                                break;
                            }

                            next++;
                        }

                        Exec(connection, tx, "UPDATE [dbo].[Asset] SET [AssetTag] = @New, [UpdatedAt] = GETUTCDATE() WHERE [Id] = @Id AND [OrganizationId] = @Org",
                            P("@New", SqlDbType.NVarChar, newTag, 60), P("@Id", SqlDbType.Int, item.AssetId), P("@Org", SqlDbType.Int, organizationId));
                        Exec(connection, tx, "INSERT INTO [dbo].[AssetTagAlias] ([OrganizationId],[AssetId],[BatchId],[AliasTag],[NewTag],[IsActive],[CreatedAt]) VALUES (@Org,@Id,@Batch,@Old,@New,1,SYSUTCDATETIME())",
                            P("@Org", SqlDbType.Int, organizationId), P("@Id", SqlDbType.Int, item.AssetId), P("@Batch", SqlDbType.Int, batchId),
                            P("@Old", SqlDbType.NVarChar, item.OldTag, 60), P("@New", SqlDbType.NVarChar, newTag, 60));
                        nextByPrefix[item.Prefix] = next + 1;
                        outcome.Items.Add(new AssetTagRenamePlanItem { AssetId = item.AssetId, AssetName = item.AssetName, DepartmentId = item.DepartmentId, OldTag = item.OldTag, Prefix = item.Prefix, NewTag = newTag });
                    }

                    if (conflicts.Count > 0)
                    {
                        tx.Rollback();
                        outcome.Items.Clear();
                        outcome.Message = "Nothing was renamed: " + conflicts.Count + " asset(s) changed since the preview (" + string.Join("; ", conflicts.Take(MaxConflictsReported)) + "). Run the preview again.";
                        return outcome;
                    }

                    foreach (var pair in nextByPrefix)
                    {
                        Exec(connection, tx, @"
IF EXISTS (SELECT 1 FROM [dbo].[AssetTagSequence] WHERE [OrganizationId] = @Org AND [Prefix] = @Prefix)
    UPDATE [dbo].[AssetTagSequence] SET [LastValue] = CASE WHEN [LastValue] > @Last THEN [LastValue] ELSE @Last END, [UpdatedAt] = SYSUTCDATETIME() WHERE [OrganizationId] = @Org AND [Prefix] = @Prefix;
ELSE
    INSERT INTO [dbo].[AssetTagSequence] ([OrganizationId],[Prefix],[LastValue],[UpdatedAt]) VALUES (@Org,@Prefix,@Last,SYSUTCDATETIME());",
                            P("@Org", SqlDbType.Int, organizationId), P("@Prefix", SqlDbType.NVarChar, pair.Key, 20), P("@Last", SqlDbType.Int, pair.Value - 1));
                    }

                    Exec(connection, tx, "UPDATE [dbo].[AssetTagRenameBatch] SET [AssetCount] = @Count WHERE [Id] = @Batch",
                        P("@Count", SqlDbType.Int, outcome.Items.Count), P("@Batch", SqlDbType.Int, batchId));
                    tx.Commit();
                    outcome.Success = true;
                    outcome.BatchId = batchId;
                    outcome.Message = "Renamed " + outcome.Items.Count + " asset tag(s) in batch " + batchId + ". Old tags still work for scans and search.";
                    return outcome;
                }
                catch
                {
                    SafeRollback(tx);
                    throw;
                }
            }
        }

        public AssetTagRenameOutcome Undo(int organizationId, int batchId, string actorUserId)
        {
            var outcome = new AssetTagRenameOutcome { BatchId = batchId };
            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    AcquireTenantLock(connection, tx, organizationId);
                    var status = Scalar(connection, tx,
                        "SELECT [Status] FROM [dbo].[AssetTagRenameBatch] WITH (UPDLOCK) WHERE [Id] = @Batch AND [OrganizationId] = @Org",
                        P("@Batch", SqlDbType.Int, batchId), P("@Org", SqlDbType.Int, organizationId)) as string;
                    if (status == null)
                    {
                        tx.Rollback();
                        outcome.Message = "Batch not found.";
                        return outcome;
                    }

                    if (!string.Equals(status, "Applied", StringComparison.OrdinalIgnoreCase))
                    {
                        tx.Rollback();
                        outcome.Message = "Batch " + batchId + " is already " + status + ".";
                        return outcome;
                    }

                    var aliases = new List<Tuple<int, int, string, string>>();
                    using (var command = Command(connection, tx, "SELECT [Id],[AssetId],[AliasTag],[NewTag] FROM [dbo].[AssetTagAlias] WITH (UPDLOCK) WHERE [BatchId] = @Batch AND [OrganizationId] = @Org AND [IsActive] = 1 ORDER BY [Id]",
                        P("@Batch", SqlDbType.Int, batchId), P("@Org", SqlDbType.Int, organizationId)))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            aliases.Add(Tuple.Create(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3)));
                        }
                    }

                    var conflicts = new List<string>();
                    foreach (var alias in aliases)
                    {
                        var current = Scalar(connection, tx, "SELECT [AssetTag] FROM [dbo].[Asset] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = @Id AND [OrganizationId] = @Org",
                            P("@Id", SqlDbType.Int, alias.Item2), P("@Org", SqlDbType.Int, organizationId)) as string;
                        if (!string.Equals(current, alias.Item4, StringComparison.Ordinal))
                        {
                            conflicts.Add("asset " + alias.Item2 + " now has tag '" + (current ?? "(none)") + "' instead of '" + alias.Item4 + "'");
                            continue;
                        }

                        if (TagTaken(connection, tx, organizationId, alias.Item3, alias.Item2, alias.Item1))
                        {
                            conflicts.Add("old tag '" + alias.Item3 + "' is now used by another asset or alias");
                            continue;
                        }

                        Exec(connection, tx, "UPDATE [dbo].[Asset] SET [AssetTag] = @Old, [UpdatedAt] = GETUTCDATE() WHERE [Id] = @Id AND [OrganizationId] = @Org",
                            P("@Old", SqlDbType.NVarChar, alias.Item3, 60), P("@Id", SqlDbType.Int, alias.Item2), P("@Org", SqlDbType.Int, organizationId));
                        Exec(connection, tx, "UPDATE [dbo].[AssetTagAlias] SET [IsActive] = 0 WHERE [Id] = @AliasId",
                            P("@AliasId", SqlDbType.Int, alias.Item1));
                        outcome.Items.Add(new AssetTagRenamePlanItem { AssetId = alias.Item2, OldTag = alias.Item3, NewTag = alias.Item4 });
                    }

                    if (conflicts.Count > 0)
                    {
                        tx.Rollback();
                        outcome.Items.Clear();
                        outcome.Message = "Undo refused, nothing was changed: " + conflicts.Count + " conflict(s) since the batch was applied (" + string.Join("; ", conflicts.Take(MaxConflictsReported)) + ").";
                        return outcome;
                    }

                    Exec(connection, tx, "UPDATE [dbo].[AssetTagRenameBatch] SET [Status] = N'Undone', [UndoneAt] = SYSUTCDATETIME(), [UndoneById] = @Actor WHERE [Id] = @Batch",
                        P("@Actor", SqlDbType.NVarChar, (object)actorUserId ?? DBNull.Value, 128), P("@Batch", SqlDbType.Int, batchId));
                    tx.Commit();
                    outcome.Success = true;
                    outcome.Message = "Batch " + batchId + " undone: " + outcome.Items.Count + " asset(s) have their exact old tags back.";
                    return outcome;
                }
                catch
                {
                    SafeRollback(tx);
                    throw;
                }
            }
        }

        public IList<AssetTagRenameBatchInfo> GetBatches(int organizationId, int take)
        {
            var list = new List<AssetTagRenameBatchInfo>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT TOP (@Take) [Id],[ScopeLabel],[AssetCount],[Status],[AppliedById],[AppliedAt],[UndoneAt] FROM [dbo].[AssetTagRenameBatch] WHERE [OrganizationId] = @Org ORDER BY [Id] DESC";
                command.Parameters.Add("@Take", SqlDbType.Int).Value = Math.Max(1, take);
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new AssetTagRenameBatchInfo
                        {
                            Id = reader.GetInt32(0),
                            ScopeLabel = reader.GetString(1),
                            AssetCount = reader.GetInt32(2),
                            Status = reader.GetString(3),
                            AppliedById = reader.IsDBNull(4) ? null : reader.GetString(4),
                            AppliedAt = reader.GetDateTime(5),
                            UndoneAt = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6)
                        });
                    }
                }
            }

            return list;
        }

        public IList<AssetTagRenamePlanItem> GetBatchItems(int organizationId, int batchId)
        {
            var list = new List<AssetTagRenamePlanItem>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT al.[AssetId], a.[AssetName], al.[AliasTag], al.[NewTag] FROM [dbo].[AssetTagAlias] al LEFT JOIN [dbo].[Asset] a ON a.[Id] = al.[AssetId] WHERE al.[BatchId] = @Batch AND al.[OrganizationId] = @Org ORDER BY al.[Id]";
                command.Parameters.Add("@Batch", SqlDbType.Int).Value = batchId;
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(new AssetTagRenamePlanItem { AssetId = reader.GetInt32(0), AssetName = reader.IsDBNull(1) ? null : reader.GetString(1), OldTag = reader.GetString(2), NewTag = reader.GetString(3) });
                    }
                }
            }

            return list;
        }

        // ---- helpers ----

        private SqlConnection Open()
        {
            var connection = _connectionFactory.CreateConnection();
            connection.Open();
            return connection;
        }

        private static void AcquireTenantLock(SqlConnection connection, SqlTransaction tx, int organizationId)
        {
            using (var command = Command(connection, tx, "DECLARE @r INT; EXEC @r = sp_getapplock @Resource = @Res, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000; SELECT @r;",
                P("@Res", SqlDbType.NVarChar, "AssetTagRename:" + organizationId, 255)))
            {
                var result = Convert.ToInt32(command.ExecuteScalar());
                if (result < 0)
                {
                    throw new InvalidOperationException("Another tag rename or undo is running for this organisation. Try again in a moment.");
                }
            }
        }

        /// <summary>Locks the counter row for the rest of the transaction and returns the first number to try.</summary>
        private static int LockAndReadNext(SqlConnection connection, SqlTransaction tx, int organizationId, string prefix)
        {
            var value = Scalar(connection, tx, @"
DECLARE @last INT, @max INT;
SELECT @last = [LastValue] FROM [dbo].[AssetTagSequence] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = @Org AND [Prefix] = @Prefix;
SELECT @max = ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(t.[Tag], LEN(@Prefix) + 2, 20))), 0)
FROM (SELECT a.[AssetTag] AS [Tag] FROM [dbo].[Asset] a WHERE a.[OrganizationId] = @Org AND a.[AssetTag] LIKE @Prefix + N'-%'
      UNION ALL SELECT al.[AliasTag] FROM [dbo].[AssetTagAlias] al WHERE al.[OrganizationId] = @Org AND al.[IsActive] = 1 AND al.[AliasTag] LIKE @Prefix + N'-%') t
WHERE LEN(t.[Tag]) > LEN(@Prefix) + 1 AND SUBSTRING(t.[Tag], LEN(@Prefix) + 2, 20) NOT LIKE N'%[^0-9]%';
SELECT (CASE WHEN ISNULL(@last, 0) > @max THEN ISNULL(@last, 0) ELSE @max END) + 1;",
                P("@Org", SqlDbType.Int, organizationId), P("@Prefix", SqlDbType.NVarChar, prefix, 20));
            return Convert.ToInt32(value);
        }

        private static bool TagTaken(SqlConnection connection, SqlTransaction tx, int organizationId, string tag, int? exceptAssetId, int? exceptAliasId)
        {
            var count = Scalar(connection, tx, @"
SELECT (SELECT COUNT(*) FROM [dbo].[Asset] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = @Org AND [AssetTag] = @Tag AND (@ExAsset IS NULL OR [Id] <> @ExAsset))
     + (SELECT COUNT(*) FROM [dbo].[AssetTagAlias] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = @Org AND [IsActive] = 1 AND [AliasTag] = @Tag AND (@ExAlias IS NULL OR [Id] <> @ExAlias))",
                P("@Org", SqlDbType.Int, organizationId), P("@Tag", SqlDbType.NVarChar, tag, 60),
                P("@ExAsset", SqlDbType.Int, exceptAssetId.HasValue ? (object)exceptAssetId.Value : DBNull.Value),
                P("@ExAlias", SqlDbType.Int, exceptAliasId.HasValue ? (object)exceptAliasId.Value : DBNull.Value));
            return Convert.ToInt32(count) > 0;
        }

        private static SqlParameter P(string name, SqlDbType type, object value, int size = 0)
        {
            var p = size > 0 ? new SqlParameter(name, type, size) : new SqlParameter(name, type);
            p.Value = value ?? DBNull.Value;
            return p;
        }

        private static SqlCommand Command(SqlConnection connection, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            var command = connection.CreateCommand();
            command.Transaction = tx;
            command.CommandText = sql;
            command.CommandTimeout = 120;
            command.Parameters.AddRange(parameters);
            return command;
        }

        private static object Scalar(SqlConnection connection, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (var command = Command(connection, tx, sql, parameters))
            {
                var value = command.ExecuteScalar();
                return value == DBNull.Value ? null : value;
            }
        }

        private static void Exec(SqlConnection connection, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (var command = Command(connection, tx, sql, parameters))
            {
                command.ExecuteNonQuery();
            }
        }

        private static void SafeRollback(SqlTransaction tx)
        {
            try
            {
                if (tx.Connection != null)
                {
                    tx.Rollback();
                }
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static string EscapeLike(string value)
        {
            return (value ?? string.Empty).Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        private static string Truncate(string value, int max)
        {
            return value.Length <= max ? value : value.Substring(0, max);
        }
    }
}