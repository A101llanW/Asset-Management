using System;
using System.Data;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Identity
{
    public class UserAccountRepository
    {
        private readonly ISqlConnectionFactory _connectionFactory;

        public UserAccountRepository(ISqlConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public void Insert(ApplicationUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            if (string.IsNullOrWhiteSpace(user.Id))
            {
                user.Id = Guid.NewGuid().ToString();
            }

            if (user.CreatedAt == default(DateTime))
            {
                user.CreatedAt = DateTime.UtcNow;
            }

            user.IsActive = true;

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO [Users]
    ([Id],[Email],[EmailConfirmed],[PasswordHash],[SecurityStamp],[PhoneNumber],[PhoneNumberConfirmed],[TwoFactorEnabled],[MfaMethod],[TwoFactorCode],[TwoFactorExpiryUtc],
     [PrivacyAcceptedAt],[TermsAcceptedAt],[PrivacyVersion],[TermsVersion],
     [LockoutEndDateUtc],[LockoutEnabled],[AccessFailedCount],[UserName],
     [EmployeeNumber],[FirstName],[LastName],[Phone],[DepartmentId],[PositionTitle],[IsActive],[RoleId],[OrganizationId],[CreatedAt],[UpdatedAt])
VALUES
    (@Id,@Email,@EmailConfirmed,@PasswordHash,@SecurityStamp,@PhoneNumber,@PhoneNumberConfirmed,@TwoFactorEnabled,@MfaMethod,@TwoFactorCode,@TwoFactorExpiryUtc,
     @PrivacyAcceptedAt,@TermsAcceptedAt,@PrivacyVersion,@TermsVersion,
     @LockoutEndDateUtc,@LockoutEnabled,@AccessFailedCount,@UserName,
     @EmployeeNumber,@FirstName,@LastName,@Phone,@DepartmentId,@PositionTitle,@IsActive,@RoleId,@OrganizationId,@CreatedAt,@UpdatedAt)";
                    AddUserParameters(command, user);
                    command.ExecuteNonQuery();
                }
            }
        }

        public void Update(ApplicationUser user)
        {
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            user.UpdatedAt = DateTime.UtcNow;

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
UPDATE [Users] SET
    [Email]=@Email,[EmailConfirmed]=@EmailConfirmed,[PasswordHash]=@PasswordHash,[SecurityStamp]=@SecurityStamp,[PhoneNumber]=@PhoneNumber,[PhoneNumberConfirmed]=@PhoneNumberConfirmed,
    [TwoFactorEnabled]=@TwoFactorEnabled,[MfaMethod]=@MfaMethod,[TwoFactorCode]=@TwoFactorCode,[TwoFactorExpiryUtc]=@TwoFactorExpiryUtc,
    [PrivacyAcceptedAt]=@PrivacyAcceptedAt,[TermsAcceptedAt]=@TermsAcceptedAt,[PrivacyVersion]=@PrivacyVersion,[TermsVersion]=@TermsVersion,
    [LockoutEndDateUtc]=@LockoutEndDateUtc,[LockoutEnabled]=@LockoutEnabled,[AccessFailedCount]=@AccessFailedCount,[UserName]=@UserName,
    [EmployeeNumber]=@EmployeeNumber,[FirstName]=@FirstName,[LastName]=@LastName,[Phone]=@Phone,[DepartmentId]=@DepartmentId,[PositionTitle]=@PositionTitle,
    [IsActive]=@IsActive,[RoleId]=@RoleId,[OrganizationId]=@OrganizationId,[UpdatedAt]=@UpdatedAt
WHERE [Id]=@Id";
                    AddUserParameters(command, user);
                    command.ExecuteNonQuery();
                }
            }
        }

        public ApplicationUser FindById(string userId)
        {
            return FindUser("WHERE [Id]=@Id", "@Id", userId);
        }

        public ApplicationUser FindById(string userId, int organizationId)
        {
            var user = FindById(userId);
            if (user == null)
            {
                return null;
            }

            if (!user.OrganizationId.HasValue || user.OrganizationId.Value != organizationId)
            {
                return null;
            }

            return user;
        }

        public ApplicationUser FindByEmail(string email)
        {
            return FindUserByNormalizedEmail(email, null);
        }

        public ApplicationUser FindActiveUserByEmail(string email)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT TOP 1 * FROM [Users] WHERE LOWER(LTRIM(RTRIM([Email])))=LOWER(LTRIM(RTRIM(@Email))) AND [IsActive]=1 ORDER BY [Id]";
                    AddParameter(command, "@Email", email);
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapUser(reader) : null;
                    }
                }
            }
        }

        public ApplicationUser FindByEmailAndOrganization(string email, int organizationId)
        {
            return FindUserByNormalizedEmail(email, organizationId);
        }

        public int CountActiveUsersInOrganization(int organizationId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM [Users] WHERE [OrganizationId] = @OrganizationId AND [IsActive] = 1";
                    AddParameter(command, "@OrganizationId", organizationId);
                    return Convert.ToInt32(command.ExecuteScalar());
                }
            }
        }

        public ApplicationUser FindPlatformAdminByEmail(string email)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT TOP 1 * FROM [Users] WHERE LOWER(LTRIM(RTRIM([Email])))=LOWER(LTRIM(RTRIM(@Email))) AND [OrganizationId] IS NULL AND [IsActive]=1 ORDER BY [Id]";
                    AddParameter(command, "@Email", email);
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapUser(reader) : null;
                    }
                }
            }
        }

        public string FindRoleNameByUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
SELECT r.[Name]
FROM [Users] u
INNER JOIN [Roles] r ON r.[Id] = u.[RoleId]
    AND (r.[OrganizationId] IS NULL OR r.[OrganizationId] = u.[OrganizationId])
WHERE u.[Id]=@Id";
                    AddParameter(command, "@Id", userId);
                    var result = command.ExecuteScalar();
                    return result == null || result == DBNull.Value ? null : result.ToString();
                }
            }
        }

        public int? FindOrganizationIdBySlug(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug))
            {
                return null;
            }

            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText =
                        "SELECT TOP 1 [Id] FROM [Organization] WHERE LOWER(LTRIM(RTRIM([Slug])))=LOWER(LTRIM(RTRIM(@Slug))) AND [IsActive]=1 ORDER BY [Id]";
                    AddParameter(command, "@Slug", slug.Trim());
                    var result = command.ExecuteScalar();
                    return result == null || result == DBNull.Value ? (int?)null : Convert.ToInt32(result);
                }
            }
        }

        private ApplicationUser FindUser(string whereClause, string parameterName, string parameterValue)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP 1 * FROM [Users] " + whereClause;
                    AddParameter(command, parameterName, parameterValue);
                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapUser(reader) : null;
                    }
                }
            }
        }

        private ApplicationUser FindUserByNormalizedEmail(string email, int? organizationId)
        {
            using (var connection = _connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = organizationId.HasValue
                        ? "SELECT TOP 1 * FROM [Users] WHERE LOWER(LTRIM(RTRIM([Email])))=LOWER(LTRIM(RTRIM(@Email))) AND [OrganizationId]=@OrganizationId ORDER BY [Id]"
                        : "SELECT TOP 1 * FROM [Users] WHERE LOWER(LTRIM(RTRIM([Email])))=LOWER(LTRIM(RTRIM(@Email))) ORDER BY [Id]";
                    AddParameter(command, "@Email", email);
                    if (organizationId.HasValue)
                    {
                        AddParameter(command, "@OrganizationId", organizationId.Value);
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        return reader.Read() ? MapUser(reader) : null;
                    }
                }
            }
        }

        private static ApplicationUser MapUser(IDataRecord record)
        {
            return new ApplicationUser
            {
                Id = record["Id"].ToString(),
                Email = GetString(record, "Email"),
                EmailConfirmed = GetBool(record, "EmailConfirmed"),
                PasswordHash = GetString(record, "PasswordHash"),
                SecurityStamp = GetString(record, "SecurityStamp"),
                PhoneNumber = GetString(record, "PhoneNumber"),
                PhoneNumberConfirmed = GetBool(record, "PhoneNumberConfirmed"),
                TwoFactorEnabled = GetBool(record, "TwoFactorEnabled"),
                MfaMethod = GetString(record, "MfaMethod"),
                TwoFactorCode = GetString(record, "TwoFactorCode"),
                TwoFactorExpiryUtc = GetNullableDateTime(record, "TwoFactorExpiryUtc"),
                PrivacyAcceptedAt = GetNullableDateTime(record, "PrivacyAcceptedAt"),
                TermsAcceptedAt = GetNullableDateTime(record, "TermsAcceptedAt"),
                PrivacyVersion = GetString(record, "PrivacyVersion"),
                TermsVersion = GetString(record, "TermsVersion"),
                LockoutEndDateUtc = GetNullableDateTime(record, "LockoutEndDateUtc"),
                LockoutEnabled = GetBool(record, "LockoutEnabled"),
                AccessFailedCount = GetInt(record, "AccessFailedCount"),
                UserName = GetString(record, "UserName"),
                EmployeeNumber = GetString(record, "EmployeeNumber"),
                FirstName = GetString(record, "FirstName"),
                LastName = GetString(record, "LastName"),
                Phone = GetString(record, "Phone"),
                DepartmentId = GetNullableInt(record, "DepartmentId"),
                PositionTitle = GetString(record, "PositionTitle"),
                IsActive = GetBool(record, "IsActive"),
                RoleId = GetNullableInt(record, "RoleId"),
                OrganizationId = GetNullableInt(record, "OrganizationId"),
                CreatedAt = GetDateTime(record, "CreatedAt"),
                UpdatedAt = GetNullableDateTime(record, "UpdatedAt")
            };
        }

        private static void AddUserParameters(IDbCommand command, ApplicationUser user)
        {
            AddParameter(command, "@Id", user.Id);
            AddParameter(command, "@Email", user.Email);
            AddParameter(command, "@EmailConfirmed", user.EmailConfirmed);
            AddParameter(command, "@PasswordHash", user.PasswordHash);
            AddParameter(command, "@SecurityStamp", user.SecurityStamp);
            AddParameter(command, "@PhoneNumber", user.PhoneNumber);
            AddParameter(command, "@PhoneNumberConfirmed", user.PhoneNumberConfirmed);
            AddParameter(command, "@TwoFactorEnabled", user.TwoFactorEnabled);
            AddParameter(command, "@MfaMethod", user.MfaMethod);
            AddParameter(command, "@TwoFactorCode", user.TwoFactorCode);
            AddParameter(command, "@TwoFactorExpiryUtc", user.TwoFactorExpiryUtc);
            AddParameter(command, "@PrivacyAcceptedAt", user.PrivacyAcceptedAt);
            AddParameter(command, "@TermsAcceptedAt", user.TermsAcceptedAt);
            AddParameter(command, "@PrivacyVersion", user.PrivacyVersion);
            AddParameter(command, "@TermsVersion", user.TermsVersion);
            AddParameter(command, "@LockoutEndDateUtc", user.LockoutEndDateUtc);
            AddParameter(command, "@LockoutEnabled", user.LockoutEnabled);
            AddParameter(command, "@AccessFailedCount", user.AccessFailedCount);
            AddParameter(command, "@UserName", user.UserName);
            AddParameter(command, "@EmployeeNumber", user.EmployeeNumber);
            AddParameter(command, "@FirstName", user.FirstName);
            AddParameter(command, "@LastName", user.LastName);
            AddParameter(command, "@Phone", user.Phone);
            AddParameter(command, "@DepartmentId", user.DepartmentId);
            AddParameter(command, "@PositionTitle", user.PositionTitle);
            AddParameter(command, "@IsActive", user.IsActive);
            AddParameter(command, "@RoleId", user.RoleId);
            AddParameter(command, "@OrganizationId", user.OrganizationId);
            AddParameter(command, "@CreatedAt", user.CreatedAt);
            AddParameter(command, "@UpdatedAt", user.UpdatedAt);
        }

        private static void AddParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string GetString(IDataRecord record, string name)
        {
            var value = record[name];
            return value == DBNull.Value ? null : value.ToString();
        }

        private static bool GetBool(IDataRecord record, string name)
        {
            var value = record[name];
            return value != DBNull.Value && Convert.ToBoolean(value);
        }

        private static int GetInt(IDataRecord record, string name)
        {
            return Convert.ToInt32(record[name]);
        }

        private static int? GetNullableInt(IDataRecord record, string name)
        {
            var value = record[name];
            if (value == DBNull.Value)
            {
                return null;
            }

            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return null;
            }
        }

        private static DateTime GetDateTime(IDataRecord record, string name)
        {
            return Convert.ToDateTime(record[name]);
        }

        private static DateTime? GetNullableDateTime(IDataRecord record, string name)
        {
            var value = record[name];
            return value == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(value);
        }
    }
}
