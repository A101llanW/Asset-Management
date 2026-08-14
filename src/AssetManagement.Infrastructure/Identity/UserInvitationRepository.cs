using System;
using System.Collections.Generic;
using System.Data;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Identity
{
    public class UserInvitationRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public UserInvitationRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public int Create(UserInvitation invitation)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO [UserInvitation]
    ([TokenHash],[OrganizationId],[InvitedByUserId],[Email],[RoleId],[DepartmentId],[ExpiresAtUtc],[CreatedAtUtc])
VALUES
    (@TokenHash,@OrganizationId,@InvitedByUserId,@Email,@RoleId,@DepartmentId,@ExpiresAtUtc,@CreatedAtUtc);
SELECT CAST(SCOPE_IDENTITY() AS INT);";
                    command.Parameters.Add(CreateParameter(command, "@TokenHash", invitation.TokenHash));
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", invitation.OrganizationId));
                    command.Parameters.Add(CreateParameter(command, "@InvitedByUserId", invitation.InvitedByUserId));
                    command.Parameters.Add(CreateParameter(command, "@Email", invitation.Email));
                    command.Parameters.Add(CreateParameter(command, "@RoleId", invitation.RoleId));
                    command.Parameters.Add(CreateParameter(command, "@DepartmentId", invitation.DepartmentId));
                    command.Parameters.Add(CreateParameter(command, "@ExpiresAtUtc", invitation.ExpiresAtUtc));
                    command.Parameters.Add(CreateParameter(command, "@CreatedAtUtc", invitation.CreatedAtUtc));
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public UserInvitation FindValidByTokenHash(string tokenHash)
        {
            if (string.IsNullOrWhiteSpace(tokenHash))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT TOP 1
    [Id],[TokenHash],[OrganizationId],[InvitedByUserId],[Email],[RoleId],[DepartmentId],
    [ExpiresAtUtc],[UsedAtUtc],[UsedByUserId],[CreatedAtUtc]
FROM [UserInvitation]
WHERE [TokenHash]=@TokenHash
  AND [UsedAtUtc] IS NULL
  AND [ExpiresAtUtc] > @NowUtc
ORDER BY [CreatedAtUtc] DESC";
                    command.Parameters.Add(CreateParameter(command, "@TokenHash", tokenHash));
                    command.Parameters.Add(CreateParameter(command, "@NowUtc", DateTime.UtcNow));
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return MapInvitation(reader);
                    }
                }
            }
        }

        public UserInvitation TryMarkUsed(string tokenHash)
        {
            if (string.IsNullOrWhiteSpace(tokenHash))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE [UserInvitation]
SET [UsedAtUtc]=@UsedAtUtc
OUTPUT
    INSERTED.[Id], INSERTED.[TokenHash], INSERTED.[OrganizationId], INSERTED.[InvitedByUserId],
    INSERTED.[Email], INSERTED.[RoleId], INSERTED.[DepartmentId], INSERTED.[ExpiresAtUtc],
    INSERTED.[UsedAtUtc], INSERTED.[UsedByUserId], INSERTED.[CreatedAtUtc]
WHERE [TokenHash]=@TokenHash
  AND [UsedAtUtc] IS NULL
  AND [ExpiresAtUtc] > @NowUtc";
                    command.Parameters.Add(CreateParameter(command, "@TokenHash", tokenHash));
                    command.Parameters.Add(CreateParameter(command, "@UsedAtUtc", DateTime.UtcNow));
                    command.Parameters.Add(CreateParameter(command, "@NowUtc", DateTime.UtcNow));
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return MapInvitation(reader);
                    }
                }
            }
        }

        public void SetUsedByUserId(string tokenHash, string usedByUserId)
        {
            if (string.IsNullOrWhiteSpace(tokenHash) || string.IsNullOrWhiteSpace(usedByUserId))
            {
                return;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE [UserInvitation]
SET [UsedByUserId]=@UsedByUserId
WHERE [TokenHash]=@TokenHash";
                    command.Parameters.Add(CreateParameter(command, "@TokenHash", tokenHash));
                    command.Parameters.Add(CreateParameter(command, "@UsedByUserId", usedByUserId));
                    command.ExecuteNonQuery();
                }
            }
        }

        public UserInvitation TryConsume(string tokenHash, string usedByUserId)
        {
            if (string.IsNullOrWhiteSpace(tokenHash))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE [UserInvitation]
SET [UsedAtUtc]=@UsedAtUtc, [UsedByUserId]=@UsedByUserId
OUTPUT
    INSERTED.[Id], INSERTED.[TokenHash], INSERTED.[OrganizationId], INSERTED.[InvitedByUserId],
    INSERTED.[Email], INSERTED.[RoleId], INSERTED.[DepartmentId], INSERTED.[ExpiresAtUtc],
    INSERTED.[UsedAtUtc], INSERTED.[UsedByUserId], INSERTED.[CreatedAtUtc]
WHERE [TokenHash]=@TokenHash
  AND [UsedAtUtc] IS NULL
  AND [ExpiresAtUtc] > @NowUtc";
                    command.Parameters.Add(CreateParameter(command, "@TokenHash", tokenHash));
                    command.Parameters.Add(CreateParameter(command, "@UsedAtUtc", DateTime.UtcNow));
                    command.Parameters.Add(CreateParameter(command, "@UsedByUserId", usedByUserId));
                    command.Parameters.Add(CreateParameter(command, "@NowUtc", DateTime.UtcNow));
                    using (var reader = command.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            return null;
                        }

                        return MapInvitation(reader);
                    }
                }
            }
        }

        public IList<UserInvitationListRow> GetByOrganization(int organizationId, int skip, int take)
        {
            var rows = new List<UserInvitationListRow>();
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT
    ui.[Id],
    ui.[Email],
    r.[Name] AS [RoleName],
    d.[Name] AS [DepartmentName],
    ui.[ExpiresAtUtc],
    ui.[UsedAtUtc],
    ui.[CreatedAtUtc]
FROM [UserInvitation] ui
LEFT JOIN [Roles] r ON r.[Id] = ui.[RoleId]
LEFT JOIN [Department] d ON d.[Id] = ui.[DepartmentId]
WHERE ui.[OrganizationId]=@OrganizationId
ORDER BY ui.[CreatedAtUtc] DESC
OFFSET @Skip ROWS FETCH NEXT @Take ROWS ONLY";
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", organizationId));
                    command.Parameters.Add(CreateParameter(command, "@Skip", skip));
                    command.Parameters.Add(CreateParameter(command, "@Take", take));
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new UserInvitationListRow
                            {
                                Id = reader.GetInt32(0),
                                Email = reader.IsDBNull(1) ? null : reader.GetString(1),
                                RoleName = reader.IsDBNull(2) ? null : reader.GetString(2),
                                DepartmentName = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ExpiresAtUtc = reader.GetDateTime(4),
                                UsedAtUtc = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                                CreatedAtUtc = reader.GetDateTime(6)
                            });
                        }
                    }
                }
            }

            return rows;
        }

        public int CountByOrganization(int organizationId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"SELECT COUNT(1) FROM [UserInvitation] WHERE [OrganizationId]=@OrganizationId";
                    command.Parameters.Add(CreateParameter(command, "@OrganizationId", organizationId));
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        private static UserInvitation MapInvitation(IDataReader reader)
        {
            return new UserInvitation
            {
                Id = reader.GetInt32(0),
                TokenHash = reader.GetString(1),
                OrganizationId = reader.GetInt32(2),
                InvitedByUserId = reader.GetString(3),
                Email = reader.IsDBNull(4) ? null : reader.GetString(4),
                RoleId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                DepartmentId = reader.IsDBNull(6) ? (int?)null : reader.GetInt32(6),
                ExpiresAtUtc = reader.GetDateTime(7),
                UsedAtUtc = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                UsedByUserId = reader.IsDBNull(9) ? null : reader.GetString(9),
                CreatedAtUtc = reader.GetDateTime(10)
            };
        }

        private static IDbDataParameter CreateParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            return parameter;
        }
    }

    public class UserInvitationListRow
    {
        public int Id { get; set; }

        public string Email { get; set; }

        public string RoleName { get; set; }

        public string DepartmentName { get; set; }

        public DateTime ExpiresAtUtc { get; set; }

        public DateTime? UsedAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
