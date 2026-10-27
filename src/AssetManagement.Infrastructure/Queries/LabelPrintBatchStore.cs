using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>rel1022a: SQL side of the label print log (tables from migration 105; history from AuditLog).</summary>
    public class LabelPrintBatchStore : ILabelPrintBatchStore
    {
        private const string UserNameSql = "COALESCE(NULLIF(LTRIM(RTRIM(ISNULL({0}.[FirstName], N'') + N' ' + ISNULL({0}.[LastName], N''))), N''), {0}.[UserName])";

        private readonly ISqlConnectionFactory _connectionFactory;

        public LabelPrintBatchStore(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<LabelPrintAssetInfo> GetPrintableAssets(int organizationId, ICollection<int> assetIds)
        {
            var result = new List<LabelPrintAssetInfo>();
            var ids = (assetIds ?? new int[0]).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return result;
            }

            foreach (var chunk in Chunk(ids, 500))
            {
                using (var connection = Open())
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT a.[Id], a.[AssetTag], a.[AssetName], a.[DepartmentId], CAST(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = 2 THEN 1 ELSE 0 END AS bit) FROM [dbo].[Asset] a LEFT JOIN [dbo].[Department] d ON d.[Id] = a.[DepartmentId] WHERE a.[OrganizationId] = @Org AND a.[IsActive] = 1 AND a.[Id] IN (" + AddIntList(command, "@a", chunk) + ")";
                    command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            result.Add(new LabelPrintAssetInfo
                            {
                                AssetId = reader.GetInt32(0),
                                AssetTag = reader.IsDBNull(1) ? null : reader.GetString(1),
                                AssetName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                DepartmentId = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3),
                                DepartmentIsActiveClass = !reader.IsDBNull(4) && reader.GetBoolean(4)
                            });
                        }
                    }
                }
            }

            return result;
        }

        public IDictionary<int, int> GetPriorPrintCounts(int organizationId, ICollection<int> assetIds)
        {
            var result = new Dictionary<int, int>();
            var ids = (assetIds ?? new int[0]).Where(x => x > 0).Distinct().ToList();
            if (ids.Count == 0)
            {
                return result;
            }

            foreach (var chunk in Chunk(ids, 500))
            {
                using (var connection = Open())
                using (var command = connection.CreateCommand())
                {
                    var names = new List<string>();
                    for (var i = 0; i < chunk.Count; i++)
                    {
                        names.Add("@e" + i);
                        command.Parameters.Add("@e" + i, SqlDbType.NVarChar, 100).Value = chunk[i].ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    command.CommandText = @"
SELECT al.[EntityId], COUNT(*)
FROM [dbo].[AuditLog] al
WHERE al.[OrganizationId] = @Org AND al.[EntityType] = N'Asset' AND al.[EntityId] IN (" + string.Join(",", names) + @")
  AND al.[Action] IN (@ActPrint, @ActReprint)
GROUP BY al.[EntityId]";
                    command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                    command.Parameters.Add("@ActPrint", SqlDbType.NVarChar, 200).Value = LabelPrintAuditActions.Print;
                    command.Parameters.Add("@ActReprint", SqlDbType.NVarChar, 200).Value = LabelPrintAuditActions.Reprint;
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            int id;
                            if (!reader.IsDBNull(0) && int.TryParse(reader.GetString(0), out id))
                            {
                                result[id] = reader.GetInt32(1);
                            }
                        }
                    }
                }
            }

            return result;
        }

        public int CreateBatch(LabelPrintBatchCreate batch)
        {
            if (batch == null || batch.Assets == null || batch.Assets.Count == 0)
            {
                throw new ArgumentException("A print batch needs at least one asset.", "batch");
            }

            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                try
                {
                    int batchId;
                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = tx;
                        command.CommandText = @"
INSERT INTO [dbo].[LabelPrintBatch]
    ([OrganizationId], [Source], [GroupLabel], [FilterDescription], [AssetCount], [PrintedCount], [Status], [IsReprint], [ReprintOfBatchId], [TemplateId], [CodeType], [CreatedById], [CreatedAt], [IsCleared])
VALUES
    (@Org, @Source, @GroupLabel, @Filter, @AssetCount, 0, N'Queued', @IsReprint, @ReprintOf, @TemplateId, @CodeType, @CreatedById, @CreatedAt, 0);
SELECT CAST(SCOPE_IDENTITY() AS int);";
                        command.Parameters.Add("@Org", SqlDbType.Int).Value = batch.OrganizationId;
                        command.Parameters.Add("@Source", SqlDbType.NVarChar, 40).Value = batch.Source ?? "BulkPrint";
                        command.Parameters.Add("@GroupLabel", SqlDbType.NVarChar, 400).Value = (object)batch.GroupLabel ?? DBNull.Value;
                        command.Parameters.Add("@Filter", SqlDbType.NVarChar, 2000).Value = (object)batch.FilterDescription ?? DBNull.Value;
                        command.Parameters.Add("@AssetCount", SqlDbType.Int).Value = batch.Assets.Count;
                        command.Parameters.Add("@IsReprint", SqlDbType.Bit).Value = batch.IsReprint;
                        command.Parameters.Add("@ReprintOf", SqlDbType.Int).Value = batch.ReprintOfBatchId.HasValue ? (object)batch.ReprintOfBatchId.Value : DBNull.Value;
                        command.Parameters.Add("@TemplateId", SqlDbType.NVarChar, 100).Value = (object)batch.TemplateId ?? DBNull.Value;
                        command.Parameters.Add("@CodeType", SqlDbType.NVarChar, 20).Value = (object)batch.CodeType ?? DBNull.Value;
                        command.Parameters.Add("@CreatedById", SqlDbType.NVarChar, 256).Value = (object)batch.CreatedById ?? DBNull.Value;
                        command.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = batch.CreatedAtUtc == default(DateTime) ? DateTime.UtcNow : batch.CreatedAtUtc;
                        batchId = Convert.ToInt32(command.ExecuteScalar());
                    }

                    var order = 0;
                    foreach (var asset in batch.Assets)
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = tx;
                            command.CommandText = @"
INSERT INTO [dbo].[LabelPrintBatchItem] ([OrganizationId], [BatchId], [AssetId], [AssetTag], [SortOrder], [PrintCount])
SELECT @Org, @Batch, a.[Id], a.[AssetTag], @Sort, 0
FROM [dbo].[Asset] a
WHERE a.[Id] = @Asset AND a.[OrganizationId] = @Org
  AND NOT EXISTS (SELECT 1 FROM [dbo].[LabelPrintBatchItem] i WHERE i.[BatchId] = @Batch AND i.[AssetId] = @Asset);";
                            command.Parameters.Add("@Org", SqlDbType.Int).Value = batch.OrganizationId;
                            command.Parameters.Add("@Batch", SqlDbType.Int).Value = batchId;
                            command.Parameters.Add("@Asset", SqlDbType.Int).Value = asset.AssetId;
                            command.Parameters.Add("@Sort", SqlDbType.Int).Value = order++;
                            command.ExecuteNonQuery();
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.Transaction = tx;
                        command.CommandText = "UPDATE [dbo].[LabelPrintBatch] SET [AssetCount] = (SELECT COUNT(*) FROM [dbo].[LabelPrintBatchItem] WHERE [BatchId] = @Batch) WHERE [Id] = @Batch AND [OrganizationId] = @Org";
                        command.Parameters.Add("@Org", SqlDbType.Int).Value = batch.OrganizationId;
                        command.Parameters.Add("@Batch", SqlDbType.Int).Value = batchId;
                        command.ExecuteNonQuery();
                    }

                    tx.Commit();
                    return batchId;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            }
        }

        public LabelPrintBatchInfo GetBatch(int organizationId, int batchId)
        {
            return QueryBatches(organizationId, "b.[Id] = @Id", 1, cmd => cmd.Parameters.Add("@Id", SqlDbType.Int).Value = batchId).FirstOrDefault();
        }

        public IList<LabelPrintBatchInfo> GetBatches(int organizationId, bool includeCleared, int take)
        {
            return QueryBatches(organizationId, includeCleared ? "1 = 1" : "b.[IsCleared] = 0", take <= 0 ? 25 : Math.Min(take, 100), null);
        }

        public IList<LabelPrintBatchItemInfo> GetBatchItems(int organizationId, int batchId)
        {
            var result = new List<LabelPrintBatchItemInfo>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT i.[AssetId], COALESCE(a.[AssetTag], i.[AssetTag]), a.[AssetName], CAST(CASE WHEN a.[IsActive] = 1 AND a.[OrganizationId] = @Org THEN 1 ELSE 0 END AS bit), i.[PrintCount], i.[LastPrintedAt],
       a.[DepartmentId], CAST(CASE WHEN d.[IsActive] = 1 AND d.[DepartmentKind] = 2 THEN 1 ELSE 0 END AS bit)
FROM [dbo].[LabelPrintBatchItem] i
INNER JOIN [dbo].[LabelPrintBatch] b ON b.[Id] = i.[BatchId] AND b.[OrganizationId] = @Org
LEFT JOIN [dbo].[Asset] a ON a.[Id] = i.[AssetId]
LEFT JOIN [dbo].[Department] d ON d.[Id] = a.[DepartmentId]
WHERE i.[BatchId] = @Batch AND i.[OrganizationId] = @Org
ORDER BY i.[SortOrder], i.[Id]";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Batch", SqlDbType.Int).Value = batchId;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new LabelPrintBatchItemInfo
                        {
                            AssetId = reader.GetInt32(0),
                            AssetTag = reader.IsDBNull(1) ? null : reader.GetString(1),
                            AssetName = reader.IsDBNull(2) ? null : reader.GetString(2),
                            AssetIsActive = !reader.IsDBNull(3) && reader.GetBoolean(3),
                            PrintCount = reader.GetInt32(4),
                            LastPrintedAtUtc = reader.IsDBNull(5) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(5), DateTimeKind.Utc),
                            DepartmentId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                            DepartmentIsActiveClass = !reader.IsDBNull(7) && reader.GetBoolean(7)
                        });
                    }
                }
            }

            return result;
        }

        public void MarkItemPrinted(int organizationId, int batchId, int assetId, string assetTag, DateTime printedAtUtc)
        {
            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            using (var command = connection.CreateCommand())
            {
                command.Transaction = tx;
                command.CommandText = @"
IF EXISTS (SELECT 1 FROM [dbo].[LabelPrintBatch] WHERE [Id] = @Batch AND [OrganizationId] = @Org)
BEGIN
    UPDATE [dbo].[LabelPrintBatchItem] SET [PrintCount] = [PrintCount] + 1, [LastPrintedAt] = @At
    WHERE [BatchId] = @Batch AND [AssetId] = @Asset AND [OrganizationId] = @Org;
    IF @@ROWCOUNT = 0
    BEGIN
        INSERT INTO [dbo].[LabelPrintBatchItem] ([OrganizationId], [BatchId], [AssetId], [AssetTag], [SortOrder], [PrintCount], [LastPrintedAt])
        SELECT @Org, @Batch, a.[Id], COALESCE(@Tag, a.[AssetTag]), ISNULL((SELECT MAX([SortOrder]) + 1 FROM [dbo].[LabelPrintBatchItem] WHERE [BatchId] = @Batch), 0), 1, @At
        FROM [dbo].[Asset] a WHERE a.[Id] = @Asset AND a.[OrganizationId] = @Org;
    END
    UPDATE [dbo].[LabelPrintBatch]
    SET [PrintedCount] = [PrintedCount] + 1,
        [LastPrintedAt] = @At,
        [Status] = CASE WHEN [Status] = N'Queued' THEN N'Printing' ELSE [Status] END,
        [AssetCount] = (SELECT COUNT(*) FROM [dbo].[LabelPrintBatchItem] WHERE [BatchId] = @Batch)
    WHERE [Id] = @Batch AND [OrganizationId] = @Org;
END";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Batch", SqlDbType.Int).Value = batchId;
                command.Parameters.Add("@Asset", SqlDbType.Int).Value = assetId;
                command.Parameters.Add("@Tag", SqlDbType.NVarChar, 60).Value = (object)assetTag ?? DBNull.Value;
                command.Parameters.Add("@At", SqlDbType.DateTime2).Value = printedAtUtc;
                command.ExecuteNonQuery();
                tx.Commit();
            }
        }

        public void CompleteBatch(int organizationId, int batchId, bool stoppedEarly, DateTime completedAtUtc)
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE [dbo].[LabelPrintBatch]
SET [Status] = CASE WHEN @Stopped = 1 OR [PrintedCount] < [AssetCount] THEN N'Stopped' ELSE N'Completed' END,
    [CompletedAt] = @At
WHERE [Id] = @Batch AND [OrganizationId] = @Org";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Batch", SqlDbType.Int).Value = batchId;
                command.Parameters.Add("@Stopped", SqlDbType.Bit).Value = stoppedEarly;
                command.Parameters.Add("@At", SqlDbType.DateTime2).Value = completedAtUtc;
                command.ExecuteNonQuery();
            }
        }

        public bool ClearBatch(int organizationId, int batchId, string clearedById, DateTime clearedAtUtc)
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
UPDATE [dbo].[LabelPrintBatch]
SET [IsCleared] = 1, [ClearedById] = @By, [ClearedAt] = @At
WHERE [Id] = @Batch AND [OrganizationId] = @Org AND [IsCleared] = 0";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Batch", SqlDbType.Int).Value = batchId;
                command.Parameters.Add("@By", SqlDbType.NVarChar, 256).Value = (object)clearedById ?? DBNull.Value;
                command.Parameters.Add("@At", SqlDbType.DateTime2).Value = clearedAtUtc;
                return command.ExecuteNonQuery() > 0;
            }
        }

        public AssetPrintHistory GetPrintHistory(int organizationId, int assetId, int take)
        {
            var history = new AssetPrintHistory();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT COUNT(*), SUM(CASE WHEN al.[Action] = @ActReprint THEN 1 ELSE 0 END), MAX(al.[Timestamp])
FROM [dbo].[AuditLog] al
WHERE al.[OrganizationId] = @Org AND al.[EntityType] = N'Asset' AND al.[EntityId] = @Entity AND al.[Action] IN (@ActPrint, @ActReprint);

SELECT TOP (@Take) al.[Id], al.[Action], al.[Timestamp], al.[ActorUserId], " + string.Format(UserNameSql, "u") + @", al.[NewValues]
FROM [dbo].[AuditLog] al
LEFT JOIN [dbo].[Users] u ON u.[Id] = al.[ActorUserId]
WHERE al.[OrganizationId] = @Org AND al.[EntityType] = N'Asset' AND al.[EntityId] = @Entity AND al.[Action] IN (@ActPrint, @ActReprint)
ORDER BY al.[Timestamp] DESC, al.[Id] DESC;";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Entity", SqlDbType.NVarChar, 100).Value = assetId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                command.Parameters.Add("@ActPrint", SqlDbType.NVarChar, 200).Value = LabelPrintAuditActions.Print;
                command.Parameters.Add("@ActReprint", SqlDbType.NVarChar, 200).Value = LabelPrintAuditActions.Reprint;
                command.Parameters.Add("@Take", SqlDbType.Int).Value = take <= 0 ? 20 : Math.Min(take, 100);
                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        history.PrintCount = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
                        history.ReprintCount = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
                        history.LastPrintedAtUtc = reader.IsDBNull(2) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc);
                    }

                    if (reader.NextResult())
                    {
                        while (reader.Read())
                        {
                            var action = reader.IsDBNull(1) ? null : reader.GetString(1);
                            history.Entries.Add(new AssetPrintHistoryEntry
                            {
                                AuditLogId = reader.GetInt32(0),
                                Action = action,
                                IsReprint = string.Equals(action, LabelPrintAuditActions.Reprint, StringComparison.OrdinalIgnoreCase),
                                TimestampUtc = DateTime.SpecifyKind(reader.GetDateTime(2), DateTimeKind.Utc),
                                ActorUserId = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ActorName = reader.IsDBNull(4) ? null : reader.GetString(4),
                                Details = reader.IsDBNull(5) ? null : reader.GetString(5)
                            });
                        }
                    }
                }
            }

            return history;
        }

        private IList<LabelPrintBatchInfo> QueryBatches(int organizationId, string where, int take, Action<SqlCommand> addParameters)
        {
            var result = new List<LabelPrintBatchInfo>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
SELECT TOP (@Take) b.[Id], b.[OrganizationId], b.[Source], b.[GroupLabel], b.[FilterDescription], b.[AssetCount], b.[PrintedCount], b.[Status],
       b.[IsReprint], b.[ReprintOfBatchId], b.[TemplateId], b.[CodeType], b.[CreatedById], " + string.Format(UserNameSql, "cu") + @",
       b.[CreatedAt], b.[LastPrintedAt], b.[CompletedAt], b.[IsCleared], b.[ClearedById], " + string.Format(UserNameSql, "xu") + @", b.[ClearedAt]
FROM [dbo].[LabelPrintBatch] b
LEFT JOIN [dbo].[Users] cu ON cu.[Id] = b.[CreatedById]
LEFT JOIN [dbo].[Users] xu ON xu.[Id] = b.[ClearedById]
WHERE b.[OrganizationId] = @Org AND " + where + @"
ORDER BY b.[Id] DESC";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                command.Parameters.Add("@Take", SqlDbType.Int).Value = take;
                if (addParameters != null)
                {
                    addParameters(command);
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new LabelPrintBatchInfo
                        {
                            Id = reader.GetInt32(0),
                            OrganizationId = reader.GetInt32(1),
                            Source = Str(reader, 2),
                            GroupLabel = Str(reader, 3),
                            FilterDescription = Str(reader, 4),
                            AssetCount = reader.GetInt32(5),
                            PrintedCount = reader.GetInt32(6),
                            Status = Str(reader, 7),
                            IsReprint = reader.GetBoolean(8),
                            ReprintOfBatchId = reader.IsDBNull(9) ? (int?)null : reader.GetInt32(9),
                            TemplateId = Str(reader, 10),
                            CodeType = Str(reader, 11),
                            CreatedById = Str(reader, 12),
                            CreatedByName = Str(reader, 13),
                            CreatedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(14), DateTimeKind.Utc),
                            LastPrintedAtUtc = Utc(reader, 15),
                            CompletedAtUtc = Utc(reader, 16),
                            IsCleared = reader.GetBoolean(17),
                            ClearedById = Str(reader, 18),
                            ClearedByName = Str(reader, 19),
                            ClearedAtUtc = Utc(reader, 20)
                        });
                    }
                }
            }

            return result;
        }

        private static string Str(SqlDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? null : reader.GetString(index);
        }

        private static DateTime? Utc(SqlDataReader reader, int index)
        {
            return reader.IsDBNull(index) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(index), DateTimeKind.Utc);
        }

        private static string AddIntList(SqlCommand command, string prefix, IList<int> ids)
        {
            var names = new List<string>();
            for (var i = 0; i < ids.Count; i++)
            {
                names.Add(prefix + i);
                command.Parameters.Add(prefix + i, SqlDbType.Int).Value = ids[i];
            }

            return string.Join(",", names);
        }

        private static IEnumerable<List<int>> Chunk(List<int> ids, int size)
        {
            for (var i = 0; i < ids.Count; i += size)
            {
                yield return ids.Skip(i).Take(size).ToList();
            }
        }

        private SqlConnection Open()
        {
            var connection = _connectionFactory.CreateConnection();
            connection.Open();
            return connection;
        }
    }
}
