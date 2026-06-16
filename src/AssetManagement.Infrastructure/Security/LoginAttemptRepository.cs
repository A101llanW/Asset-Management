using System;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Security
{
    public class LoginAttemptRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public LoginAttemptRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void Record(string username, string ipAddress, bool success, int? organizationId, string failureReason)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO [LoginAttempts] ([Username],[IpAddress],[AttemptedAtUtc],[Success],[FailureReason],[OrganizationId])
VALUES (@Username,@IpAddress,@AttemptedAtUtc,@Success,@FailureReason,@OrganizationId)";
                    AddParameter(command, "@Username", username.Trim());
                    AddParameter(command, "@IpAddress", ipAddress ?? string.Empty);
                    AddParameter(command, "@AttemptedAtUtc", DateTime.UtcNow);
                    AddParameter(command, "@Success", success);
                    AddParameter(command, "@FailureReason", failureReason);
                    AddParameter(command, "@OrganizationId", organizationId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public int CountRecentFailedByUsername(string username, int? organizationId, DateTime sinceUtc)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return 0;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT COUNT(1) FROM [LoginAttempts]
WHERE LOWER([Username]) = LOWER(@Username)
  AND [Success] = 0
  AND [AttemptedAtUtc] >= @SinceUtc
  AND ((@OrganizationId IS NULL AND [OrganizationId] IS NULL) OR [OrganizationId] = @OrganizationId)";
                    AddParameter(command, "@Username", username.Trim());
                    AddParameter(command, "@SinceUtc", sinceUtc);
                    AddParameter(command, "@OrganizationId", organizationId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public int CountRecentFailedByIp(string ipAddress, DateTime sinceUtc)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return 0;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT COUNT(1) FROM [LoginAttempts]
WHERE [IpAddress] = @IpAddress
  AND [Success] = 0
  AND [AttemptedAtUtc] >= @SinceUtc";
                    AddParameter(command, "@IpAddress", ipAddress);
                    AddParameter(command, "@SinceUtc", sinceUtc);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public void DeleteFailedAttempts(string username, int? organizationId)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
DELETE FROM [LoginAttempts]
WHERE LOWER([Username]) = LOWER(@Username)
  AND [Success] = 0
  AND ((@OrganizationId IS NULL AND [OrganizationId] IS NULL) OR [OrganizationId] = @OrganizationId)";
                    AddParameter(command, "@Username", username.Trim());
                    AddParameter(command, "@OrganizationId", organizationId);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void ClearAllFailedAttempts()
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM [LoginAttempts] WHERE [Success] = 0";
                    command.ExecuteNonQuery();
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
