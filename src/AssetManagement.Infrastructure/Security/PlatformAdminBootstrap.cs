using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Persistence;
using Newtonsoft.Json;

namespace AssetManagement.Infrastructure.Security
{
    public enum PlatformAdminBootstrapStatus
    {
        Inactive = 0,
        AlreadyApplied = 1,
        Applied = 2,
        Refused = 3
    }

    public sealed class PlatformAdminBootstrapConfig
    {
        public string Email { get; set; }

        public string Password { get; set; }

        public bool ClearMfaEnrollment { get; set; }

        /// <summary>Optional Bootstrap:RunId - any new value forces a new run for otherwise identical keys.</summary>
        public string RunId { get; set; }

        public bool HasPassword
        {
            get { return !string.IsNullOrEmpty(Password); }
        }
    }

    public sealed class PlatformAdminBootstrapResult
    {
        public PlatformAdminBootstrapStatus Status { get; set; }

        /// <summary>Safe to log: never contains the password or a hash.</summary>
        public string Message { get; set; }

        public string UserId { get; set; }
    }

    /// <summary>
    /// rel1009i: one-time platform admin recovery at Application_Start, active ONLY when Bootstrap:PlatformAdminEmail is
    /// set in appSettings. Finds THE platform admin by role (system role "Platform Admin", OrganizationId NULL), sets the
    /// email (and the mirrored UserName), reactivates and unlocks it, optionally sets a password (app hasher + policy,
    /// RequirePasswordChange=1, new AccessToken) and optionally clears the MFA enrolment. Writes AuditLog
    /// PLATFORM_ADMIN_BOOTSTRAP; that audit row (markerKey) is the run-once marker, so the same key set does not run on every
    /// recycle. (SystemSetting cannot hold it: SystemSetting.OrganizationId is NOT NULL and there is no platform organisation.)
    /// </summary>
    public static class PlatformAdminBootstrap
    {
        public const string EmailKey = "Bootstrap:PlatformAdminEmail";
        public const string PasswordKey = "Bootstrap:PlatformAdminPassword";
        public const string ClearMfaKey = "Bootstrap:ClearMfaEnrollment";
        public const string RunIdKey = "Bootstrap:RunId";
        public const string AuditAction = "PLATFORM_ADMIN_BOOTSTRAP";
        public const string MarkerKeyPrefix = "Bootstrap.PlatformAdmin.";
        public const string PlatformAdminRoleName = "Platform Admin";

        public static bool AnyKeysPresent(NameValueCollection appSettings)
        {
            if (appSettings == null)
            {
                return false;
            }

            return appSettings.AllKeys.Any(k => k != null && k.StartsWith("Bootstrap:", StringComparison.OrdinalIgnoreCase));
        }

        public static PlatformAdminBootstrapConfig ReadConfig(NameValueCollection appSettings)
        {
            if (appSettings == null)
            {
                return null;
            }

            var email = appSettings[EmailKey];
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var clear = appSettings[ClearMfaKey];
            return new PlatformAdminBootstrapConfig
            {
                Email = email.Trim().ToLowerInvariant(),
                Password = appSettings[PasswordKey],
                RunId = (appSettings[RunIdKey] ?? string.Empty).Trim(),
                ClearMfaEnrollment = string.Equals((clear ?? string.Empty).Trim(), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals((clear ?? string.Empty).Trim(), "1", StringComparison.OrdinalIgnoreCase)
            };
        }

        /// <summary>Returns a safe error message, or null when the configuration is usable.</summary>
        public static string ValidateConfig(PlatformAdminBootstrapConfig config)
        {
            if (config == null || string.IsNullOrWhiteSpace(config.Email))
            {
                return "Bootstrap:PlatformAdminEmail is empty.";
            }

            var at = config.Email.IndexOf('@');
            if (at <= 0 || at != config.Email.LastIndexOf('@') || at == config.Email.Length - 1
                || config.Email.IndexOf('.', at) < 0 || config.Email.Any(char.IsWhiteSpace) || config.Email.Length > 256)
            {
                return "Bootstrap:PlatformAdminEmail is not a valid email address.";
            }

            if (config.HasPassword)
            {
                var errors = PasswordPolicy.Validate(config.Password).ToList();
                if (errors.Count > 0)
                {
                    return "Bootstrap:PlatformAdminPassword does not meet the password policy: " + string.Join(" ", errors);
                }
            }

            return null;
        }

        /// <summary>Marker key: hash of (email, password present, clear-MFA flag, RunId). Never the password.</summary>
        public static string BuildMarkerKey(PlatformAdminBootstrapConfig config)
        {
            var material = "v2|" + config.Email + "|" + (config.HasPassword ? "1" : "0") + "|" + (config.ClearMfaEnrollment ? "1" : "0")
                + "|" + (config.RunId ?? string.Empty);
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(material));
                var hex = BitConverter.ToString(hash).Replace("-", string.Empty);
                return MarkerKeyPrefix + hex.Substring(0, 32);
            }
        }

        /// <summary>
        /// Run-once decision. No marker (audit row for this key set) = run. With a marker: no password configured = skip;
        /// password configured = skip while the stored hash still verifies it (not yet changed) or once the user has changed
        /// the password after the run (stale keys must never reset it again). Otherwise (a different password for the same
        /// key set, not yet used) = run. Set a new Bootstrap:RunId to force another run. Stores nothing derived from the password.
        /// </summary>
        public static bool ShouldSkip(PlatformAdminBootstrapConfig config, DateTime? markerAppliedUtc, string currentPasswordHash, DateTime? lastPasswordChangeUtc)
        {
            if (config == null || !markerAppliedUtc.HasValue)
            {
                return false;
            }

            if (!config.HasPassword)
            {
                return true;
            }

            if (lastPasswordChangeUtc.HasValue && lastPasswordChangeUtc.Value > markerAppliedUtc.Value.AddSeconds(5))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(currentPasswordHash))
            {
                return false;
            }

            try
            {
                return PasswordHasher.VerifyHashedPassword(currentPasswordHash, config.Password) != PasswordVerificationResult.Failed;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        public static PlatformAdminBootstrapResult Run(ISqlConnectionFactory connectionFactory, NameValueCollection appSettings)
        {
            var config = ReadConfig(appSettings);
            if (config == null)
            {
                return new PlatformAdminBootstrapResult
                {
                    Status = PlatformAdminBootstrapStatus.Inactive,
                    Message = AnyKeysPresent(appSettings) ? "Bootstrap keys present but Bootstrap:PlatformAdminEmail is empty; nothing done." : "Not configured."
                };
            }

            var invalid = ValidateConfig(config);
            if (invalid != null)
            {
                return Refused(invalid, null);
            }

            if (connectionFactory == null)
            {
                return Refused("No database connection factory.", null);
            }

            var markerKey = BuildMarkerKey(config);
            using (var connection = connectionFactory.CreateConnection())
            {
                connection.Open();
                DateTime? markerAppliedUtc = null;
                string currentHash = null;
                DateTime? lastPasswordChange = null;
                using (var cmd = Command(connection, null,
                    "SELECT TOP 1 a.[Timestamp], u.[PasswordHash], u.[LastPasswordChange] FROM [AuditLog] a "
                    + "INNER JOIN [Users] u ON u.[Id]=a.[EntityId] "
                    + "WHERE a.[Action]=@Action AND a.[OrganizationId] IS NULL AND a.[NewValues] LIKE @Like ORDER BY a.[Id] DESC"))
                {
                    cmd.Parameters.Add(P("@Action", AuditAction));
                    cmd.Parameters.Add(P("@Like", "%\"markerKey\":\"" + markerKey + "\"%"));
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            markerAppliedUtc = reader.IsDBNull(0) ? (DateTime?)null : reader.GetDateTime(0);
                            currentHash = reader.IsDBNull(1) ? null : reader.GetString(1);
                            lastPasswordChange = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                        }
                    }
                }

                if (ShouldSkip(config, markerAppliedUtc, currentHash, lastPasswordChange))
                {
                    return new PlatformAdminBootstrapResult
                    {
                        Status = PlatformAdminBootstrapStatus.AlreadyApplied,
                        Message = "Already applied for this key set (" + markerKey + "). Remove the Bootstrap:* keys from Web.config."
                    };
                }

                using (var tx = connection.BeginTransaction())
                {
                    var admins = new List<string[]>();
                    using (var cmd = Command(connection, tx,
                        "SELECT u.[Id], u.[Email], CAST(u.[IsActive] AS int) FROM [Users] u INNER JOIN [Roles] r ON r.[Id]=u.[RoleId] "
                        + "WHERE u.[OrganizationId] IS NULL AND r.[IsSystemRole]=1 AND r.[Name]=@Role"))
                    {
                        cmd.Parameters.Add(P("@Role", PlatformAdminRoleName));
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                admins.Add(new[]
                                {
                                    reader.GetString(0),
                                    reader.IsDBNull(1) ? null : reader.GetString(1),
                                    reader.GetInt32(2).ToString()
                                });
                            }
                        }
                    }

                    if (admins.Count != 1)
                    {
                        tx.Rollback();
                        return Refused("Expected exactly one platform admin (system role 'Platform Admin', OrganizationId NULL); found " + admins.Count + ". Nothing changed.", null);
                    }

                    var userId = admins[0][0];
                    var oldEmail = admins[0][1];
                    var wasActive = admins[0][2] == "1";

                    var duplicates = Convert.ToInt32(Scalar(connection, tx,
                        "SELECT COUNT(*) FROM [Users] WHERE [Id]<>@Id AND [IsActive]=1 AND LOWER(LTRIM(RTRIM([Email])))=@Email",
                        P("@Id", userId), P("@Email", config.Email)));
                    if (duplicates > 0)
                    {
                        tx.Rollback();
                        return Refused("Another active user already uses " + config.Email + "; nothing changed. Pick a different address or change the other user.", userId);
                    }

                    var now = DateTime.UtcNow;
                    Execute(connection, tx,
                        "UPDATE [Users] SET [Email]=@Email, "
                        + "[UserName]=CASE WHEN [UserName] IS NULL OR LOWER(LTRIM(RTRIM([UserName])))=LOWER(LTRIM(RTRIM(ISNULL([Email],N'')))) THEN @Email ELSE [UserName] END, "
                        + "[IsActive]=1, [LockoutEndDateUtc]=NULL, [AccessFailedCount]=0, [UpdatedAt]=GETUTCDATE() WHERE [Id]=@Id",
                        P("@Email", config.Email), P("@Id", userId));

                    if (config.HasPassword)
                    {
                        Execute(connection, tx,
                            "UPDATE [Users] SET [PasswordHash]=@Hash, [RequirePasswordChange]=1, [LastPasswordChange]=@Now, [AccessToken]=@Token WHERE [Id]=@Id",
                            P("@Now", now),
                            P("@Hash", PasswordHasher.HashPassword(config.Password)),
                            P("@Token", SecurePasswordGenerator.GenerateAccessToken()),
                            P("@Id", userId));
                    }

                    if (config.ClearMfaEnrollment)
                    {
                        Execute(connection, tx,
                            "UPDATE [Users] SET [TwoFactorEnabled]=0, [MfaMethod]=NULL, [TwoFactorCode]=NULL, [TwoFactorExpiryUtc]=NULL WHERE [Id]=@Id",
                            P("@Id", userId));
                    }

                    var clearedAttempts = Execute(connection, tx,
                        "IF OBJECT_ID(N'[LoginAttempts]', N'U') IS NOT NULL "
                        + "DELETE FROM [LoginAttempts] WHERE [Success]=0 AND LOWER(LTRIM(RTRIM([Username]))) IN (@Email, LOWER(LTRIM(RTRIM(ISNULL(@Old, N'')))))",
                        P("@Email", config.Email), P("@Old", (object)oldEmail ?? DBNull.Value));
                    Execute(connection, tx,
                        "IF OBJECT_ID(N'[AuthFlowRateLimitFailures]', N'U') IS NOT NULL DELETE FROM [AuthFlowRateLimitFailures] WHERE [ScopeKey] IN (N'mfa-send|'+@Id, N'mfa-verify|'+@Id); "
                        + "IF OBJECT_ID(N'[AuthFlowRateLimitState]', N'U') IS NOT NULL DELETE FROM [AuthFlowRateLimitState] WHERE [ScopeKey] IN (N'mfa-send|'+@Id, N'mfa-verify|'+@Id);",
                        P("@Id", userId));

                    var oldValues = JsonConvert.SerializeObject(new { email = oldEmail, isActive = wasActive });
                    var newValues = JsonConvert.SerializeObject(new
                    {
                        email = config.Email,
                        isActive = true,
                        reactivated = !wasActive,
                        passwordSet = config.HasPassword,
                        requirePasswordChange = config.HasPassword,
                        accessTokenRotated = config.HasPassword,
                        mfaEnrollmentCleared = config.ClearMfaEnrollment,
                        failedLoginAttemptsCleared = clearedAttempts < 0 ? 0 : clearedAttempts,
                        markerKey = markerKey
                    });
                    Execute(connection, tx,
                        "INSERT INTO [AuditLog] ([ActorUserId],[Action],[EntityType],[EntityId],[OldValues],[NewValues],[Timestamp],[IPAddress],[CreatedAt],[UpdatedAt],[IsActive],[OrganizationId]) "
                        + "VALUES (NULL,@Action,N'User',@Id,@Old,@New,@Now,N'app-start',@Now,@Now,1,NULL)",
                        P("@Action", AuditAction), P("@Id", userId), P("@Old", oldValues), P("@New", newValues), P("@Now", now));

                    tx.Commit();
                    return new PlatformAdminBootstrapResult
                    {
                        Status = PlatformAdminBootstrapStatus.Applied,
                        UserId = userId,
                        Message = "Platform admin " + userId + " set to " + config.Email
                            + (wasActive ? string.Empty : ", reactivated")
                            + ", unlocked"
                            + (config.HasPassword ? ", password set (change required at next sign-in), sessions ended" : string.Empty)
                            + (config.ClearMfaEnrollment ? ", MFA enrolment cleared" : string.Empty)
                            + ". Remove the Bootstrap:* keys from Web.config now."
                    };
                }
            }
        }

        private static PlatformAdminBootstrapResult Refused(string message, string userId)
        {
            return new PlatformAdminBootstrapResult { Status = PlatformAdminBootstrapStatus.Refused, Message = message, UserId = userId };
        }

        private static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        private static SqlCommand Command(SqlConnection connection, SqlTransaction tx, string sql)
        {
            var cmd = connection.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = sql;
            return cmd;
        }

        private static object Scalar(SqlConnection connection, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (var cmd = Command(connection, tx, sql))
            {
                cmd.Parameters.AddRange(parameters);
                var value = cmd.ExecuteScalar();
                return value == DBNull.Value ? null : value;
            }
        }

        private static int Execute(SqlConnection connection, SqlTransaction tx, string sql, params SqlParameter[] parameters)
        {
            using (var cmd = Command(connection, tx, sql))
            {
                cmd.Parameters.AddRange(parameters);
                return cmd.ExecuteNonQuery();
            }
        }
    }
}
