using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>
    /// Server-side paged reads of AssetMovement. IX_AssetMovement_Org_Occurred serves the org list in time order,
    /// IX_AssetMovement_Asset the per-asset history and IX_AssetMovement_Org_Batch the batch view.
    /// </summary>
    public class AssetMovementQueryRepository : IAssetMovementQueryRepository
    {
        private const string WhereSql = @"
WHERE m.[OrganizationId] = @OrganizationId
  AND (@FromUtc IS NULL OR m.[OccurredAtUtc] >= @FromUtc)
  AND (@ToUtc IS NULL OR m.[OccurredAtUtc] < @ToUtc)
  AND (@MovementType IS NULL OR m.[MovementType] = @MovementType)
  AND (@AssetId IS NULL OR m.[AssetId] = @AssetId)
  AND (@BatchId IS NULL OR m.[BatchId] = @BatchId)
  AND (@DepartmentId IS NULL OR m.[FromDepartmentId] = @DepartmentId OR m.[ToDepartmentId] = @DepartmentId)
  AND (@ScopedDepartmentId IS NULL
       OR m.[FromDepartmentId] = @ScopedDepartmentId OR m.[ToDepartmentId] = @ScopedDepartmentId
       OR EXISTS (SELECT 1 FROM [Department] sd
                  WHERE sd.[ParentDepartmentId] = @ScopedDepartmentId
                    AND (sd.[Id] = m.[FromDepartmentId] OR sd.[Id] = m.[ToDepartmentId])))
  AND (@Search IS NULL OR EXISTS (SELECT 1 FROM [Asset] sa
                                  WHERE sa.[Id] = m.[AssetId]
                                    AND (sa.[AssetTag] LIKE @Search OR sa.[AssetName] LIKE @Search OR sa.[SerialNumber] LIKE @Search)))";

        private const string CountSql = "SELECT COUNT(*) FROM [AssetMovement] m" + WhereSql + ";";

        private const string PageSql = @"
SELECT m.[Id], m.[AssetId], a.[AssetTag], a.[AssetName],
       m.[FromDepartmentId], fd.[Name] AS FromDepartmentName,
       m.[ToDepartmentId], td.[Name] AS ToDepartmentName,
       m.[FromCustodianUserId], NULLIF(LTRIM(RTRIM(ISNULL(fu.[FirstName], N'') + N' ' + ISNULL(fu.[LastName], N''))), N'') AS FromCustodianName,
       m.[ToCustodianUserId], NULLIF(LTRIM(RTRIM(ISNULL(tu.[FirstName], N'') + N' ' + ISNULL(tu.[LastName], N''))), N'') AS ToCustodianName,
       m.[MovementType], m.[BatchId], m.[PerformedByUserId],
       COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(pu.[FirstName], N'') + N' ' + ISNULL(pu.[LastName], N''))), N''), pu.[UserName]) AS PerformedByName,
       m.[OccurredAtUtc], m.[Notes], m.[SourceRef]
FROM [AssetMovement] m
INNER JOIN [Asset] a ON a.[Id] = m.[AssetId]
LEFT JOIN [Department] fd ON fd.[Id] = m.[FromDepartmentId]
LEFT JOIN [Department] td ON td.[Id] = m.[ToDepartmentId]
LEFT JOIN [Users] fu ON fu.[Id] = m.[FromCustodianUserId]
LEFT JOIN [Users] tu ON tu.[Id] = m.[ToCustodianUserId]
LEFT JOIN [Users] pu ON pu.[Id] = m.[PerformedByUserId]" + WhereSql + @"
ORDER BY m.[OccurredAtUtc] DESC, m.[Id] DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY;";

        private readonly ISqlConnectionFactory _connectionFactory;

        public AssetMovementQueryRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public AssetMovementQueryResult Query(
            int organizationId,
            int? scopedDepartmentId,
            AssetMovementFilterVm filter,
            DateTime? fromUtc,
            DateTime? toUtcExclusive,
            int skip,
            int take,
            bool includeTotal)
        {
            filter = filter ?? new AssetMovementFilterVm();
            var result = new AssetMovementQueryResult();
            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }

                    if (includeTotal)
                    {
                        using (var count = connection.CreateCommand())
                        {
                            count.CommandText = CountSql;
                            AddParameters(count, organizationId, scopedDepartmentId, filter, fromUtc, toUtcExclusive);
                            result.TotalCount = Convert.ToInt32(count.ExecuteScalar());
                        }
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = PageSql;
                        AddParameters(command, organizationId, scopedDepartmentId, filter, fromUtc, toUtcExclusive);
                        command.Parameters.Add("@Skip", SqlDbType.Int).Value = Math.Max(0, skip);
                        command.Parameters.Add("@Take", SqlDbType.Int).Value = Math.Max(1, take);
                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                result.Rows.Add(new AssetMovementRowVm
                                {
                                    Id = reader.GetInt32(0),
                                    AssetId = reader.GetInt32(1),
                                    AssetTag = Str(reader, 2),
                                    AssetName = Str(reader, 3),
                                    FromDepartmentId = Int(reader, 4),
                                    FromDepartmentName = Str(reader, 5),
                                    ToDepartmentId = Int(reader, 6),
                                    ToDepartmentName = Str(reader, 7),
                                    FromCustodianUserId = Str(reader, 8),
                                    FromCustodianName = Str(reader, 9),
                                    ToCustodianUserId = Str(reader, 10),
                                    ToCustodianName = Str(reader, 11),
                                    MovementType = (AssetMovementType)Convert.ToInt32(reader.GetValue(12)),
                                    BatchId = Str(reader, 13),
                                    PerformedByUserId = Str(reader, 14),
                                    PerformedByName = Str(reader, 15),
                                    OccurredAtUtc = DateTime.SpecifyKind(reader.GetDateTime(16), DateTimeKind.Utc),
                                    Notes = Str(reader, 17),
                                    SourceRef = Str(reader, 18)
                                });
                            }
                        }
                    }

                    if (!includeTotal)
                    {
                        result.TotalCount = result.Rows.Count;
                    }
                }
            }
            catch (SqlException ex) when (OptionalTableProbe.IsMissingTable(ex))
            {
                result = new AssetMovementQueryResult { Unavailable = true };
            }

            return result;
        }

        private static void AddParameters(
            SqlCommand command,
            int organizationId,
            int? scopedDepartmentId,
            AssetMovementFilterVm filter,
            DateTime? fromUtc,
            DateTime? toUtcExclusive)
        {
            command.Parameters.Add("@OrganizationId", SqlDbType.Int).Value = organizationId;
            command.Parameters.Add("@FromUtc", SqlDbType.DateTime2).Value = (object)fromUtc ?? DBNull.Value;
            command.Parameters.Add("@ToUtc", SqlDbType.DateTime2).Value = (object)toUtcExclusive ?? DBNull.Value;
            command.Parameters.Add("@MovementType", SqlDbType.Int).Value = filter.MovementType.HasValue ? (object)(int)filter.MovementType.Value : DBNull.Value;
            command.Parameters.Add("@AssetId", SqlDbType.Int).Value = (object)filter.AssetId ?? DBNull.Value;
            command.Parameters.Add("@BatchId", SqlDbType.NVarChar, 64).Value = string.IsNullOrWhiteSpace(filter.BatchId) ? (object)DBNull.Value : filter.BatchId.Trim();
            command.Parameters.Add("@DepartmentId", SqlDbType.Int).Value = (object)filter.DepartmentId ?? DBNull.Value;
            command.Parameters.Add("@ScopedDepartmentId", SqlDbType.Int).Value = (object)scopedDepartmentId ?? DBNull.Value;
            command.Parameters.Add("@Search", SqlDbType.NVarChar, 210).Value = string.IsNullOrWhiteSpace(filter.AssetSearch)
                ? (object)DBNull.Value
                : "%" + EscapeLike(filter.AssetSearch.Trim()) + "%";
        }

        private static string EscapeLike(string value)
        {
            if (value.Length > 200)
            {
                value = value.Substring(0, 200);
            }

            return value.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        }

        private static string Str(IDataRecord r, int i)
        {
            return r.IsDBNull(i) ? null : r.GetString(i);
        }

        private static int? Int(IDataRecord r, int i)
        {
            return r.IsDBNull(i) ? (int?)null : r.GetInt32(i);
        }
    }
}
