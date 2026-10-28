using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Queries
{
    /// <summary>Hidden how-to video page: SQL side (table HelpVideo, migration 109). Files live under App_Data.</summary>
    public class HelpVideoStore : IHelpVideoStore
    {
        private const string SelectColumns = @"v.[Id], v.[OrganizationId], v.[TopicSlug], v.[Title], v.[OriginalFileName], v.[StoredFileName],
v.[ContentType], v.[SizeBytes], v.[UploadedById],
COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(u.[FirstName], N'') + N' ' + ISNULL(u.[LastName], N''))), N''), u.[UserName]),
v.[UploadedAt]
FROM [dbo].[HelpVideo] v
LEFT JOIN [dbo].[Users] u ON u.[Id] = v.[UploadedById]";

        private readonly ISqlConnectionFactory _connectionFactory;

        public HelpVideoStore(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public IList<HelpVideoInfo> GetVisible(int organizationId)
        {
            var result = new List<HelpVideoInfo>();
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + SelectColumns
                    + " WHERE v.[OrganizationId] = @Org OR v.[OrganizationId] IS NULL ORDER BY v.[TopicSlug], v.[OrganizationId] DESC";
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(Read(reader));
                    }
                }
            }

            return result;
        }

        public HelpVideoInfo GetVisibleById(int organizationId, int id)
        {
            using (var connection = Open())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + SelectColumns
                    + " WHERE v.[Id] = @Id AND (v.[OrganizationId] = @Org OR v.[OrganizationId] IS NULL)";
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                using (var reader = command.ExecuteReader())
                {
                    return reader.Read() ? Read(reader) : null;
                }
            }
        }

        public HelpVideoSaveResult Save(HelpVideoSave video)
        {
            if (video == null)
            {
                throw new ArgumentNullException("video");
            }

            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                int? existingId = null;
                string previousFile = null;
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = tx;
                    command.CommandText = "SELECT [Id], [StoredFileName] FROM [dbo].[HelpVideo] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = @Org AND [TopicSlug] = @Slug";
                    command.Parameters.Add("@Org", SqlDbType.Int).Value = video.OrganizationId;
                    command.Parameters.Add("@Slug", SqlDbType.NVarChar, 80).Value = video.TopicSlug;
                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            existingId = reader.GetInt32(0);
                            previousFile = reader.GetString(1);
                        }
                    }
                }

                int id;
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = tx;
                    if (existingId.HasValue)
                    {
                        command.CommandText = @"UPDATE [dbo].[HelpVideo] SET [Title] = @Title, [OriginalFileName] = @Original, [StoredFileName] = @Stored,
[ContentType] = @Type, [SizeBytes] = @Size, [UploadedById] = @By, [UploadedAt] = @At WHERE [Id] = @Id";
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = existingId.Value;
                    }
                    else
                    {
                        command.CommandText = @"INSERT INTO [dbo].[HelpVideo] ([OrganizationId], [TopicSlug], [Title], [OriginalFileName], [StoredFileName], [ContentType], [SizeBytes], [UploadedById], [UploadedAt])
VALUES (@Org, @Slug, @Title, @Original, @Stored, @Type, @Size, @By, @At); SELECT CAST(SCOPE_IDENTITY() AS int);";
                        command.Parameters.Add("@Org", SqlDbType.Int).Value = video.OrganizationId;
                        command.Parameters.Add("@Slug", SqlDbType.NVarChar, 80).Value = video.TopicSlug;
                    }

                    command.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = video.Title;
                    command.Parameters.Add("@Original", SqlDbType.NVarChar, 260).Value = video.OriginalFileName;
                    command.Parameters.Add("@Stored", SqlDbType.NVarChar, 100).Value = video.StoredFileName;
                    command.Parameters.Add("@Type", SqlDbType.NVarChar, 50).Value = video.ContentType;
                    command.Parameters.Add("@Size", SqlDbType.BigInt).Value = video.SizeBytes;
                    command.Parameters.Add("@By", SqlDbType.NVarChar, 256).Value = (object)video.UploadedById ?? DBNull.Value;
                    command.Parameters.Add("@At", SqlDbType.DateTime2).Value = video.UploadedAtUtc;
                    if (existingId.HasValue)
                    {
                        command.ExecuteNonQuery();
                        id = existingId.Value;
                    }
                    else
                    {
                        id = (int)command.ExecuteScalar();
                    }
                }

                tx.Commit();
                return new HelpVideoSaveResult
                {
                    Id = id,
                    Replaced = existingId.HasValue,
                    PreviousStoredFileName = previousFile
                };
            }
        }

        public HelpVideoInfo Delete(int organizationId, int id)
        {
            using (var connection = Open())
            using (var tx = connection.BeginTransaction(IsolationLevel.ReadCommitted))
            {
                HelpVideoInfo existing;
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = tx;
                    command.CommandText = "SELECT " + SelectColumns.Replace("FROM [dbo].[HelpVideo] v", "FROM [dbo].[HelpVideo] v WITH (UPDLOCK)")
                        + " WHERE v.[Id] = @Id AND v.[OrganizationId] = @Org";
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                    using (var reader = command.ExecuteReader())
                    {
                        existing = reader.Read() ? Read(reader) : null;
                    }
                }

                if (existing == null)
                {
                    tx.Rollback();
                    return null;
                }

                using (var command = connection.CreateCommand())
                {
                    command.Transaction = tx;
                    command.CommandText = "DELETE FROM [dbo].[HelpVideo] WHERE [Id] = @Id AND [OrganizationId] = @Org";
                    command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                    command.Parameters.Add("@Org", SqlDbType.Int).Value = organizationId;
                    command.ExecuteNonQuery();
                }

                tx.Commit();
                return existing;
            }
        }

        private static HelpVideoInfo Read(SqlDataReader reader)
        {
            return new HelpVideoInfo
            {
                Id = reader.GetInt32(0),
                OrganizationId = reader.IsDBNull(1) ? (int?)null : reader.GetInt32(1),
                TopicSlug = reader.GetString(2),
                Title = reader.GetString(3),
                OriginalFileName = reader.GetString(4),
                StoredFileName = reader.GetString(5),
                ContentType = reader.GetString(6),
                SizeBytes = reader.GetInt64(7),
                UploadedById = reader.IsDBNull(8) ? null : reader.GetString(8),
                UploadedByName = reader.IsDBNull(9) ? null : reader.GetString(9),
                UploadedAtUtc = DateTime.SpecifyKind(reader.GetDateTime(10), DateTimeKind.Utc)
            };
        }

        private SqlConnection Open()
        {
            var connection = _connectionFactory.CreateConnection();
            connection.Open();
            return connection;
        }
    }
}
