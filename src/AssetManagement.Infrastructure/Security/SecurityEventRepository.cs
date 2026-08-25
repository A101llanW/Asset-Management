using System;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Security
{
    public class SecurityEventRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public SecurityEventRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void Record(string eventType, string email, string ipAddress, int? organizationId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO [SecurityEvents] ([EventType],[Email],[IpAddress],[OrganizationId],[CreatedAtUtc])
VALUES (@EventType,@Email,@IpAddress,@OrganizationId,@CreatedAtUtc)";
                    AddParameter(command, "@EventType", eventType);
                    AddParameter(command, "@Email", email);
                    AddParameter(command, "@IpAddress", ipAddress);
                    AddParameter(command, "@OrganizationId", organizationId);
                    AddParameter(command, "@CreatedAtUtc", DateTime.UtcNow);
                    command.ExecuteNonQuery();
                }
            }
        }

        public int CountRecent(string eventType, string ipAddress, DateTime sinceUtc)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT COUNT(1) FROM [SecurityEvents]
WHERE [EventType]=@EventType AND [IpAddress]=@IpAddress AND [CreatedAtUtc]>=@SinceUtc";
                    AddParameter(command, "@EventType", eventType);
                    AddParameter(command, "@IpAddress", ipAddress);
                    AddParameter(command, "@SinceUtc", sinceUtc);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
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
