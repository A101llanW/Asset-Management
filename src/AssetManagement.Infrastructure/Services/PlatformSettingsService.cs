using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Services
{
    public class PlatformSettingsService : IPlatformSettingsService
    {
        // The web registers this service per request, so the memo removes the 18-45 identical
        // SystemSetting lookups a single page made. The TTL bounds staleness for the long-lived
        // Runner instance. SetSetting clears the memo.
        private static readonly TimeSpan MemoLifetime = TimeSpan.FromSeconds(30);
        private readonly ISqlConnectionFactory _connectionFactory;
        private readonly Dictionary<string, KeyValuePair<DateTime, string>> _memo =
            new Dictionary<string, KeyValuePair<DateTime, string>>(StringComparer.OrdinalIgnoreCase);
        private readonly object _memoLock = new object();

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

            var trimmedKey = key.Trim();
            KeyValuePair<DateTime, string> cached;
            lock (_memoLock)
            {
                if (_memo.TryGetValue(trimmedKey, out cached) && cached.Key > DateTime.UtcNow)
                {
                    return cached.Value ?? FallbackSetting(trimmedKey, defaultValue);
                }
            }

            var stored = ReadStoredSetting(trimmedKey);
            if (stored.Key)
            {
                lock (_memoLock)
                {
                    _memo[trimmedKey] = new KeyValuePair<DateTime, string>(DateTime.UtcNow.Add(MemoLifetime), stored.Value);
                }
            }

            return stored.Value ?? FallbackSetting(trimmedKey, defaultValue);
        }

        private static string FallbackSetting(string key, string defaultValue)
        {
            var webConfigValue = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(webConfigValue) ? defaultValue : webConfigValue;
        }

        /// <summary>Key = lookup succeeded (cacheable); Value = non-blank stored value or null.</summary>
        private KeyValuePair<bool, string> ReadStoredSetting(string key)
        {
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
                        AddParameter(command, "@SettingKey", key);
                        var value = command.ExecuteScalar();
                        if (value != null && value != DBNull.Value)
                        {
                            var text = Convert.ToString(value);
                            if (!string.IsNullOrWhiteSpace(text))
                            {
                                return new KeyValuePair<bool, string>(true, text);
                            }
                        }
                    }
                }

                return new KeyValuePair<bool, string>(true, null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Platform setting lookup failed for " + key + ": " + ex.Message);
                return new KeyValuePair<bool, string>(false, null);
            }
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

            lock (_memoLock)
            {
                _memo.Clear();
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
