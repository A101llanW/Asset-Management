using System;
using System.Configuration;
using System.Data;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Services
{
    public class PlatformSettingsService : IPlatformSettingsService
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public PlatformSettingsService(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public string GetSetting(string key, string defaultValue)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return defaultValue;
            }

            try
            {
                using (var connection = _connectionFactory.CreateConnection())
                {
                    connection.Open();
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
SELECT TOP 1 [SettingValue]
FROM [SystemSetting]
WHERE [SettingKey] = @SettingKey AND [OrganizationId] IS NULL";
                        AddParameter(command, "@SettingKey", key.Trim());
                        var value = command.ExecuteScalar();
                        if (value != null && value != DBNull.Value)
                        {
                            var text = Convert.ToString(value);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                return text;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Platform setting lookup failed for " + key + ": " + ex.Message);
            }

            var webConfigValue = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(webConfigValue) ? defaultValue : webConfigValue;
        }

        public void SetSetting(string key, string value, string description)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
IF EXISTS (SELECT 1 FROM [SystemSetting] WHERE [SettingKey]=@SettingKey AND [OrganizationId] IS NULL)
BEGIN
    UPDATE [SystemSetting]
    SET [SettingValue]=@SettingValue, [Description]=@Description, [UpdatedAt]=GETUTCDATE()
    WHERE [SettingKey]=@SettingKey AND [OrganizationId] IS NULL
END
ELSE
BEGIN
    INSERT INTO [SystemSetting] ([SettingKey],[SettingValue],[Description],[OrganizationId],[CreatedAt],[UpdatedAt])
    VALUES (@SettingKey,@SettingValue,@Description,NULL,GETUTCDATE(),GETUTCDATE())
END";
                    AddParameter(command, "@SettingKey", key.Trim());
                    AddParameter(command, "@SettingValue", value ?? string.Empty);
                    AddParameter(command, "@Description", description ?? string.Empty);
                    command.ExecuteNonQuery();
                }
            }
        }

        public string GetExternalBaseUrl()
        {
            var configured = GetSetting("ExternalBaseUrl", null);
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim().TrimEnd('/');
            }

            return ConfigurationManager.AppSettings["ExternalBaseUrl"];
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
