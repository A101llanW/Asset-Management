using System;

using System.Collections.Generic;

using System.Configuration;

using System.Security.Cryptography;

using AssetManagement.Application.Contracts;

using AssetManagement.Application.Contracts.Security;

using AssetManagement.Application.Security;

using AssetManagement.Infrastructure.Identity;

using AssetManagement.Infrastructure.Persistence;

using AssetManagement.Infrastructure.Security;



namespace AssetManagement.Infrastructure.Services

{

    public class AccountSecurityService : IAccountSecurityService

    {

        private const string AuthForgotRequestEventType = "AUTH_FORGOT_REQUEST";

        private const string AuthLockoutEventType = "AUTH_LOCKOUT";

        private const int MaxForgotPasswordRequests = 5;

        private const int ForgotPasswordWindowMinutes = 10;

        private const int MfaCodeExpiryMinutes = 10;

        private const int MaxFailedAttempts = 5;

        private const int LockoutDurationMinutes = 30;

        private const int MaxIpFailures = 20;

        private const int IpWindowMinutes = 15;



        private readonly UserAccountRepository _users;

        private readonly SecurityEventRepository _securityEvents;

        private readonly LoginAttemptRepository _loginAttempts;

        private readonly IEmailService _emailService;



        public AccountSecurityService(

            ISqlConnectionFactory connectionFactory,

            IEmailService emailService)

        {

            _users = new UserAccountRepository(connectionFactory);

            _securityEvents = new SecurityEventRepository(connectionFactory);

            _loginAttempts = new LoginAttemptRepository(connectionFactory);

            _emailService = emailService;

        }



        public bool RequiresPrivilegedMfa(string userId)

        {

            string roleName;

            bool isSystemRole;

            if (!_users.TryGetRoleInfoByUserId(userId, out roleName, out isSystemRole))

            {

                return false;

            }

            return SystemRoleNames.IsPlatformAdmin(roleName, isSystemRole)

                || SystemRoleNames.IsCompanyAdmin(roleName, isSystemRole);

        }



        public bool RequiresMfa(string userId)

        {

            if (DeploymentSecuritySettings.RequireMfaForAllUsers)

            {

                return !string.IsNullOrWhiteSpace(userId);

            }

            return RequiresPrivilegedMfa(userId);

        }



        public bool UserNeedsLegalConsent(string userId)

        {

            var user = _users.FindById(userId);

            if (user == null)

            {

                return false;

            }



            var relationship = LegalPolicyDefaults.ResolveFromRoleAndOrganization(

                _users.FindRoleNameByUserId(userId),

                user.OrganizationId);

            var expectedPrivacy = LegalPolicyDefaults.GetPrivacyVersion(relationship);

            var expectedTerms = LegalPolicyDefaults.GetTermsVersion(relationship);

            var requiresPrivacyUpdate = !user.PrivacyAcceptedAt.HasValue

                || !string.Equals(user.PrivacyVersion, expectedPrivacy, StringComparison.Ordinal);

            var requiresTermsUpdate = !user.TermsAcceptedAt.HasValue

                || !string.Equals(user.TermsVersion, expectedTerms, StringComparison.Ordinal);

            return requiresPrivacyUpdate || requiresTermsUpdate;

        }



        public void RecordLegalAcceptance(string userId)

        {

            var user = _users.FindById(userId);

            if (user == null)

            {

                return;

            }



            var relationship = LegalPolicyDefaults.ResolveFromRoleAndOrganization(

                _users.FindRoleNameByUserId(userId),

                user.OrganizationId);

            var acceptedAt = DateTime.UtcNow;

            var privacyVersion = LegalPolicyDefaults.GetPrivacyVersion(relationship);

            var termsVersion = LegalPolicyDefaults.GetTermsVersion(relationship);



            if (!user.PrivacyAcceptedAt.HasValue || !string.Equals(user.PrivacyVersion, privacyVersion, StringComparison.Ordinal))

            {

                user.PrivacyAcceptedAt = acceptedAt;

                user.PrivacyVersion = privacyVersion;

            }



            if (!user.TermsAcceptedAt.HasValue || !string.Equals(user.TermsVersion, termsVersion, StringComparison.Ordinal))

            {

                user.TermsAcceptedAt = acceptedAt;

                user.TermsVersion = termsVersion;

            }



            _users.Update(user);

        }



        public bool SendMfaCode(string userId)

        {

            var user = _users.FindById(userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))

            {

                return false;

            }



            var relaxed = IsMfaCodeValidationRelaxed();
            // Company SMTP when the user's organization has it on and complete; platform default otherwise.
            var mailer = _emailService == null ? null : _emailService.ForOrganization(EmailOrganizationRouting.ForUser(_users, user));

            if (!relaxed && (mailer == null || !mailer.IsConfigured))

            {

                System.Diagnostics.Trace.WriteLine(

                    "MFA code not sent: SMTP is not configured while MfaAllowAnyCode=false.");

                return false;

            }



            var code = GenerateNumericCode(6);

            user.TwoFactorCode = code;

            user.TwoFactorExpiryUtc = DateTime.UtcNow.AddMinutes(MfaCodeExpiryMinutes);

            if (string.IsNullOrWhiteSpace(user.MfaMethod))

            {

                user.MfaMethod = "Email";

            }



            _users.Update(user);



            if (relaxed)

            {

                SecurityDiagnostics.LogOneTimeCode("MFA", user.Email, code);

            }



            if (mailer != null && mailer.IsConfigured)
            {
                // Off the request path: an SMTP round trip took ~3 s on the VerifyMfa GET (up to 3 tries
                // with a 10 s timeout when the server is slow). The code is already stored and the rate
                // limit already counted; a failed send is logged (Trace + email_errors.txt) and the user
                // can use Resend code.
                var emailService = mailer;
                var to = user.Email;
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        emailService.SendMfaCodeEmail(to, code);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine("MFA email send failed: " + ex.Message);
                    }
                });
                return true;
            }

            return relaxed;

        }



        public bool IsMfaCodeValidationRelaxed()

        {

            return DeploymentSecuritySettings.MfaAllowAnyCode;

        }



        public bool ValidateMfaCode(string userId, string code)

        {

            var relaxed = ReadMfaAllowAnyCodeSetting();

            if (relaxed && !string.IsNullOrWhiteSpace(code))

            {

                SecurityDiagnostics.LogMfaDevBypass(userId);

            }



            var user = _users.FindById(userId);

            return MfaCodeValidator.Validate(
                relaxed,
                user == null ? null : user.TwoFactorCode,
                user == null ? null : user.TwoFactorExpiryUtc,
                code,
                DateTime.UtcNow);

        }



        public void EnableMfa(string userId, string method)

        {

            var user = _users.FindById(userId);

            if (user == null)

            {

                return;

            }



            user.TwoFactorEnabled = true;

            user.MfaMethod = string.IsNullOrWhiteSpace(method) ? "Email" : method;

            user.TwoFactorCode = null;

            user.TwoFactorExpiryUtc = null;

            _users.Update(user);

        }



        public void ClearMfaCode(string userId)

        {

            var user = _users.FindById(userId);

            if (user == null)

            {

                return;

            }



            user.TwoFactorCode = null;

            user.TwoFactorExpiryUtc = null;

            _users.Update(user);

        }



        public bool IsForgotPasswordRateLimited(string ipAddress)

        {

            if (string.IsNullOrWhiteSpace(ipAddress))

            {

                return false;

            }



            var sinceUtc = DateTime.UtcNow.AddMinutes(-ForgotPasswordWindowMinutes);

            var count = _securityEvents.CountRecent(AuthForgotRequestEventType, ipAddress, sinceUtc);

            return count >= MaxForgotPasswordRequests;

        }



        public void RecordForgotPasswordAttempt(string ipAddress, string email, int? organizationId)

        {

            _securityEvents.Record(AuthForgotRequestEventType, email, ipAddress, organizationId);

        }



        public string MaskEmail(string email)

        {

            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))

            {

                return email;

            }



            var parts = email.Split('@');

            var local = parts[0];

            var domain = parts[1];

            if (local.Length <= 2)

            {

                return local[0] + "***@" + domain;

            }



            return local.Substring(0, 2) + "***@" + domain;

        }



        public bool IsLoginIpRateLimited(string ipAddress)

        {

            if (!IsLoginLockoutEnabled() || string.IsNullOrWhiteSpace(ipAddress))

            {

                return false;

            }



            var sinceUtc = DateTime.UtcNow.AddMinutes(-IpWindowMinutes);

            return _loginAttempts.CountRecentFailedByIp(ipAddress, sinceUtc) >= MaxIpFailures;

        }



        public bool IsAccountLocked(string username, int? organizationId)

        {

            if (!IsLoginLockoutEnabled() || string.IsNullOrWhiteSpace(username))

            {

                return false;

            }



            var sinceUtc = DateTime.UtcNow.AddMinutes(-LockoutDurationMinutes);

            return _loginAttempts.CountRecentFailedByUsername(username, organizationId, sinceUtc) >= MaxFailedAttempts;

        }



        public DateTime? GetLockoutEndTimeUtc(string username, int? organizationId)

        {

            if (!IsAccountLocked(username, organizationId))

            {

                return null;

            }



            return DateTime.UtcNow.AddMinutes(LockoutDurationMinutes);

        }



        public int GetRemainingLoginAttempts(string username, int? organizationId)

        {

            if (!IsLoginLockoutEnabled())

            {

                return MaxFailedAttempts;

            }



            if (string.IsNullOrWhiteSpace(username))

            {

                return MaxFailedAttempts;

            }



            var sinceUtc = DateTime.UtcNow.AddMinutes(-LockoutDurationMinutes);

            var failed = _loginAttempts.CountRecentFailedByUsername(username, organizationId, sinceUtc);

            return Math.Max(0, MaxFailedAttempts - failed);

        }



        public void RecordLoginAttempt(string username, string ipAddress, bool wasSuccessful, int? organizationId, string failureReason)

        {

            if (!IsLoginLockoutEnabled())

            {

                return;

            }



            var wasAlreadyLocked = !wasSuccessful && IsAccountLocked(username, organizationId);

            _loginAttempts.Record(username, ipAddress, wasSuccessful, organizationId, failureReason);



            if (!wasSuccessful && !wasAlreadyLocked && IsAccountLocked(username, organizationId))

            {

                InvalidateSessionsForLockedAccount(username, organizationId);

                TryAlertAccountLockout(username, ipAddress, organizationId, failureReason);

            }

        }



        
        private void TryAlertAccountLockout(string username, string ipAddress, int? organizationId, string failureReason)

        {

            try

            {

                _securityEvents.Record(AuthLockoutEventType, username, ipAddress, organizationId);

            }

            catch

            {

                // Security log must not break login flow.

            }



            try

            {

                // Platform admins only, so this stays on the platform default SMTP (_emailService is the platform sender).
                var emails = _users.ListActivePlatformAdminEmails();

                if (emails == null || emails.Count == 0 || _emailService == null)

                {

                    return;

                }



                var subject = "Security alert: account lockout";

                var body = string.Format(

                    "<p>An account was locked after repeated failed login attempts.</p>"

                    + "<p><strong>Username:</strong> {0}</p>"

                    + "<p><strong>IP:</strong> {1}</p>"

                    + "<p><strong>OrganizationId:</strong> {2}</p>"

                    + "<p><strong>Reason:</strong> {3}</p>"

                    + "<p><strong>UTC:</strong> {4:u}</p>",

                    System.Security.SecurityElement.Escape(username ?? string.Empty),

                    System.Security.SecurityElement.Escape(ipAddress ?? string.Empty),

                    organizationId.HasValue ? organizationId.Value.ToString() : "(platform)",

                    System.Security.SecurityElement.Escape(failureReason ?? string.Empty),

                    DateTime.UtcNow);



                foreach (var email in emails)

                {

                    try

                    {

                        _emailService.TrySendSecurityAlert(email, subject, body);

                    }

                    catch

                    {

                        // Fail soft per recipient.

                    }

                }

            }

            catch

            {

                // Fail soft if SMTP missing or email path throws.

            }

        }


        public void ClearFailedLoginAttempts(string username, int? organizationId)

        {

            _loginAttempts.DeleteFailedAttempts(username, organizationId);

        }



        public void ClearFailedLoginAttemptsForUser(string userId)

        {

            var user = _users.FindById(userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))

            {

                return;

            }



            ClearFailedLoginAttempts(user.Email, user.OrganizationId);

        }



        public void ClearAllLoginLockouts()
        {
            _loginAttempts.ClearAllFailedAttempts();
        }

        public bool IsEmailVerificationRequired()
        {
            var setting = ConfigurationManager.AppSettings["EmailVerificationRequired"];
            return string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(setting, "1", StringComparison.OrdinalIgnoreCase);
        }

        public bool UserNeedsEmailVerification(string userId)
        {
            if (!IsEmailVerificationRequired())
            {
                return false;
            }

            var user = _users.FindById(userId);
            return user != null && user.IsActive && !user.IsEmailVerified;
        }

        public bool SendEmailVerificationCode(string userId)
        {
            var user = _users.FindById(userId);
            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            var relaxed = IsMfaCodeValidationRelaxed();
            var mailer = _emailService == null ? null : _emailService.ForOrganization(EmailOrganizationRouting.ForUser(_users, user));
            if (!relaxed && (mailer == null || !mailer.IsConfigured))
            {
                System.Diagnostics.Trace.WriteLine(
                    "Email verification code not sent: SMTP is not configured while MfaAllowAnyCode=false.");
                return false;
            }

            user.EmailVerificationCode = GenerateNumericCode(6);
            user.EmailVerificationExpiryUtc = DateTime.UtcNow.AddMinutes(30);
            _users.Update(user);

            if (mailer != null && mailer.IsConfigured)
            {
                try
                {
                    mailer.SendMfaCodeEmail(user.Email, user.EmailVerificationCode);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("Email verification send failed: " + ex.Message);
                    if (!relaxed)
                    {
                        user.EmailVerificationCode = null;
                        user.EmailVerificationExpiryUtc = null;
                        _users.Update(user);
                        return false;
                    }
                }
            }

            return relaxed;
        }

        public bool ValidateEmailVerificationCode(string userId, string code)
        {
            var user = _users.FindById(userId);
            if (user == null || string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            if (!string.Equals(user.EmailVerificationCode, code.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            if (!user.EmailVerificationExpiryUtc.HasValue || user.EmailVerificationExpiryUtc.Value < DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }

        public void MarkEmailVerified(string userId)
        {
            var user = _users.FindById(userId);
            if (user == null)
            {
                return;
            }

            user.IsEmailVerified = true;
            user.EmailVerificationCode = null;
            user.EmailVerificationExpiryUtc = null;
            _users.Update(user);
        }

        public bool SendVerificationCodeToAddress(string email, out string code)
        {
            return SendVerificationCodeToAddress(email, null, out code);
        }

        /// <summary>Sends a code to a new address; <paramref name="userId"/> picks the user's organization SMTP.</summary>
        public bool SendVerificationCodeToAddress(string email, string userId, out string code)
        {
            code = null;
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var relaxed = IsMfaCodeValidationRelaxed();
            var owner = string.IsNullOrWhiteSpace(userId) ? null : _users.FindById(userId);
            var mailer = _emailService == null ? null : _emailService.ForOrganization(EmailOrganizationRouting.ForUser(_users, owner));
            if (!relaxed && (mailer == null || !mailer.IsConfigured))
            {
                System.Diagnostics.Trace.WriteLine(
                    "Verification code not sent: SMTP is not configured while MfaAllowAnyCode=false.");
                return false;
            }

            code = GenerateNumericCode(6);

            if (mailer != null && mailer.IsConfigured)
            {
                try
                {
                    mailer.SendMfaCodeEmail(email.Trim(), code);
                    return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("Verification code send failed: " + ex.Message);
                    if (!relaxed)
                    {
                        code = null;
                        return false;
                    }
                }
            }

            return relaxed;
        }

        public void RotateUserAccessToken(string userId)
        {
            var user = _users.FindById(userId);
            if (user == null)
            {
                return;
            }

            user.AccessToken = SecurePasswordGenerator.GenerateAccessToken();
            _users.Update(user);
        }

        public int InvalidateAllActiveSessions(int? organizationId)
        {
            _users.ExpireActiveImpersonationRequests(organizationId);
            return _users.RotateAllAccessTokens(organizationId);
        }

        private void InvalidateSessionsForLockedAccount(string username, int? organizationId)
        {
            ApplicationUser user;
            if (organizationId.HasValue)
            {
                user = _users.FindByEmailAndOrganization(username, organizationId.Value);
            }
            else
            {
                user = _users.FindPlatformAdminByEmail(username)
                    ?? _users.FindActiveUserByEmail(username);
            }

            if (user != null)
            {
                RotateUserAccessToken(user.Id);
            }
        }

        private static bool ReadMfaAllowAnyCodeSetting()

        {

            return DeploymentSecuritySettings.MfaAllowAnyCode;

        }



        private static bool IsLoginLockoutEnabled()

        {

            var setting = ConfigurationManager.AppSettings["LoginLockoutEnabled"];

            if (string.IsNullOrWhiteSpace(setting))

            {

                return true;

            }



            return !string.Equals(setting.Trim(), "false", StringComparison.OrdinalIgnoreCase)

                && !string.Equals(setting.Trim(), "0", StringComparison.OrdinalIgnoreCase);

        }



        private static string GenerateNumericCode(int length)

        {

            var bytes = new byte[length];

            using (var rng = new RNGCryptoServiceProvider())

            {

                rng.GetBytes(bytes);

            }



            var chars = new char[length];

            for (var i = 0; i < length; i++)

            {

                chars[i] = (char)('0' + (bytes[i] % 10));

            }



            return new string(chars);

        }

    }

}


