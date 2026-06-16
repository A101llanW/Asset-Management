using System;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Identity
{
    public class PasswordResetRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public PasswordResetRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void CreateToken(string userId, string tokenHash, DateTime expiresAtUtc)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO [PasswordResetToken] ([UserId],[TokenHash],[ExpiresAtUtc],[CreatedAtUtc])
VALUES (@UserId,@TokenHash,@ExpiresAtUtc,@CreatedAtUtc)";
                    command.Parameters.Add(CreateParameter(command, "@UserId", userId));
                    command.Parameters.Add(CreateParameter(command, "@TokenHash", tokenHash));
                    command.Parameters.Add(CreateParameter(command, "@ExpiresAtUtc", expiresAtUtc));
                    command.Parameters.Add(CreateParameter(command, "@CreatedAtUtc", DateTime.UtcNow));
                    command.ExecuteNonQuery();
                }
            }
        }

        public bool TryConsumeToken(string userId, string tokenHash, out string consumedUserId)
        {
            consumedUserId = null;
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1 [UserId]
FROM [PasswordResetToken]
WHERE [UserId]=@UserId AND [TokenHash]=@TokenHash AND [UsedAtUtc] IS NULL AND [ExpiresAtUtc] > @NowUtc
ORDER BY [CreatedAtUtc] DESC";
                    command.Parameters.Add(CreateParameter(command, "@UserId", userId));
                    command.Parameters.Add(CreateParameter(command, "@TokenHash", tokenHash));
                    command.Parameters.Add(CreateParameter(command, "@NowUtc", DateTime.UtcNow));
                    var result = command.ExecuteScalar();
                    if (result == null || result == DBNull.Value)
                    {
                        return false;
                    }

                    consumedUserId = Convert.ToString(result);
                }

                using (var update = connection.CreateCommand())
                {
                    update.CommandText = @"
UPDATE [PasswordResetToken]
SET [UsedAtUtc]=@UsedAtUtc
WHERE [UserId]=@UserId AND [TokenHash]=@TokenHash AND [UsedAtUtc] IS NULL";
                    update.Parameters.Add(CreateParameter(update, "@UserId", userId));
                    update.Parameters.Add(CreateParameter(update, "@TokenHash", tokenHash));
                    update.Parameters.Add(CreateParameter(update, "@UsedAtUtc", DateTime.UtcNow));
                    update.ExecuteNonQuery();
                }
            }

            return true;
        }

        private static IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }
    }
}
