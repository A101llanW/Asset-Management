using System;
using System.Data;
using System.Data.SqlClient;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Email;

namespace AssetManagement.Infrastructure.Persistence
{
    /// <summary>
    /// ADO access to OrganizationSmtpSettings (migration 094). Reads tolerate the table being absent
    /// (OptionalTableProbe), so email keeps using the platform default until the migration runs.
    /// </summary>
    public class OrganizationSmtpSettingsRepository : IOrganizationSmtpSettingsStore
    {
        public const string TableName = "OrganizationSmtpSettings";

        private const string SelectSql = @"
SELECT TOP 1 [Id], [OrganizationId], [Host], [Port], [UseSsl], [Username], [PasswordEncrypted],
       [FromAddress], [FromDisplayName], [IsEnabled], [IsActive], [CreatedAt], [CreatedByUserId],
       [UpdatedAt], [UpdatedByUserId]
FROM [dbo].[OrganizationSmtpSettings]
WHERE [OrganizationId] = @OrganizationId;";

        private const string UpsertSql = @"
IF EXISTS (SELECT 1 FROM [dbo].[OrganizationSmtpSettings] WHERE [OrganizationId] = @OrganizationId)
BEGIN
    UPDATE [dbo].[OrganizationSmtpSettings]
    SET [Host] = @Host, [Port] = @Port, [UseSsl] = @UseSsl, [Username] = @Username,
        [PasswordEncrypted] = @PasswordEncrypted, [FromAddress] = @FromAddress,
        [FromDisplayName] = @FromDisplayName, [IsEnabled] = @IsEnabled, [IsActive] = @IsActive,
        [UpdatedAt] = SYSUTCDATETIME(), [UpdatedByUserId] = @ActorUserId
    WHERE [OrganizationId] = @OrganizationId;
END
ELSE
BEGIN
    INSERT INTO [dbo].[OrganizationSmtpSettings]
        ([OrganizationId], [Host], [Port], [UseSsl], [Username], [PasswordEncrypted], [FromAddress],
         [FromDisplayName], [IsEnabled], [IsActive], [CreatedAt], [CreatedByUserId])
    VALUES
        (@OrganizationId, @Host, @Port, @UseSsl, @Username, @PasswordEncrypted, @FromAddress,
         @FromDisplayName, @IsEnabled, @IsActive, SYSUTCDATETIME(), @ActorUserId);
END";

        private readonly ISqlConnectionFactory _connectionFactory;

        public OrganizationSmtpSettingsRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public bool IsAvailable()
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                return OptionalTableProbe.Exists(connection, null, TableName);
            }
        }

        public OrganizationSmtpSettingsRecord Get(int organizationId)
        {
            if (organizationId <= 0)
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                if (!OptionalTableProbe.Exists(connection, null, TableName))
                {
                    return null;
                }

                try
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = SelectSql;
                        command.Parameters.Add("@OrganizationId", SqlDbType.Int).Value = organizationId;
                        using (var reader = command.ExecuteReader(CommandBehavior.SingleRow))
                        {
                            return reader.Read() ? Map(reader) : null;
                        }
                    }
                }
                catch (SqlException ex) when (OptionalTableProbe.IsMissingTable(ex))
                {
                    return null;
                }
            }
        }

        public void Save(OrganizationSmtpSettingsRecord record, string actorUserId)
        {
            if (record == null)
            {
                throw new ArgumentNullException("record");
            }

            if (record.OrganizationId <= 0)
            {
                throw new ArgumentException("OrganizationId is required.", "record");
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                if (!OptionalTableProbe.Exists(connection, null, TableName))
                {
                    throw new InvalidOperationException("Company email settings are not available yet (database update 094 has not been applied).");
                }

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = UpsertSql;
                    command.Parameters.Add("@OrganizationId", SqlDbType.Int).Value = record.OrganizationId;
                    command.Parameters.Add("@Host", SqlDbType.NVarChar, 255).Value = DbText(record.Host);
                    command.Parameters.Add("@Port", SqlDbType.Int).Value = record.Port;
                    command.Parameters.Add("@UseSsl", SqlDbType.Bit).Value = record.UseSsl;
                    command.Parameters.Add("@Username", SqlDbType.NVarChar, 256).Value = DbText(record.Username);
                    command.Parameters.Add("@PasswordEncrypted", SqlDbType.NVarChar, 1024).Value = record.HasPassword ? (object)record.PasswordEncrypted : DBNull.Value;
                    command.Parameters.Add("@FromAddress", SqlDbType.NVarChar, 256).Value = DbText(record.FromAddress);
                    command.Parameters.Add("@FromDisplayName", SqlDbType.NVarChar, 200).Value = DbText(record.FromDisplayName);
                    command.Parameters.Add("@IsEnabled", SqlDbType.Bit).Value = record.IsEnabled;
                    command.Parameters.Add("@IsActive", SqlDbType.Bit).Value = record.IsActive;
                    command.Parameters.Add("@ActorUserId", SqlDbType.NVarChar, 128).Value = DbText(actorUserId);
                    command.ExecuteNonQuery();
                }
            }
        }

        private static object DbText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? (object)DBNull.Value : value.Trim();
        }

        private static OrganizationSmtpSettingsRecord Map(IDataRecord reader)
        {
            return new OrganizationSmtpSettingsRecord
            {
                Id = reader.GetInt32(0),
                OrganizationId = reader.GetInt32(1),
                Host = reader.IsDBNull(2) ? null : reader.GetString(2),
                Port = reader.GetInt32(3),
                UseSsl = reader.GetBoolean(4),
                Username = reader.IsDBNull(5) ? null : reader.GetString(5),
                PasswordEncrypted = reader.IsDBNull(6) ? null : reader.GetString(6),
                FromAddress = reader.IsDBNull(7) ? null : reader.GetString(7),
                FromDisplayName = reader.IsDBNull(8) ? null : reader.GetString(8),
                IsEnabled = reader.GetBoolean(9),
                IsActive = reader.GetBoolean(10),
                CreatedAt = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc),
                CreatedByUserId = reader.IsDBNull(12) ? null : reader.GetString(12),
                UpdatedAt = reader.IsDBNull(13) ? (DateTime?)null : DateTime.SpecifyKind(reader.GetDateTime(13), DateTimeKind.Utc),
                UpdatedByUserId = reader.IsDBNull(14) ? null : reader.GetString(14)
            };
        }
    }
}
