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

            var roleName = _users.FindRoleNameByUserId(userId);

            return string.Equals(roleName, "Platform Admin", StringComparison.OrdinalIgnoreCase)

                || string.Equals(roleName, "Company Admin", StringComparison.OrdinalIgnoreCase);

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

            if (!relaxed && (_emailService == null || !_emailService.IsConfigured))

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



            if (_emailService != null && _emailService.IsConfigured)

            {

                try

                {

                    _emailService.SendMfaCodeEmail(user.Email, code);

                    return true;

                }

                catch (Exception ex)

                {

                    System.Diagnostics.Trace.WriteLine("MFA email send failed: " + ex.Message);

                    if (!relaxed)

                    {

                        user.TwoFactorCode = null;

                        user.TwoFactorExpiryUtc = null;

                        _users.Update(user);

                        return false;

                    }

                }

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
            if (!relaxed && (_emailService == null || !_emailService.IsConfigured))
            {
                System.Diagnostics.Trace.WriteLine(
                    "Email verification code not sent: SMTP is not configured while MfaAllowAnyCode=false.");
                return false;
            }

            user.EmailVerificationCode = GenerateNumericCode(6);
            user.EmailVerificationExpiryUtc = DateTime.UtcNow.AddMinutes(30);
            _users.Update(user);

            if (_emailService != null && _emailService.IsConfigured)
            {
                try
                {
                    _emailService.SendMfaCodeEmail(user.Email, user.EmailVerificationCode);
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


