using System;

namespace AssetManagement.Application.Contracts.Security
{
    public interface IAccountSecurityService
    {
        bool RequiresPrivilegedMfa(string userId);

        /// <summary>
        /// True when MFA setup/verification is required at sign-in (all users in production, or privileged roles in dev).
        /// </summary>
        bool RequiresMfa(string userId);

        bool UserNeedsLegalConsent(string userId);

        void RecordLegalAcceptance(string userId);

        bool SendMfaCode(string userId);

        bool ValidateMfaCode(string userId, string code);

        /// <summary>
        /// True when MfaAllowAnyCode is explicitly enabled in app settings (E2E/dev only; off by default).
        /// </summary>
        bool IsMfaCodeValidationRelaxed();

        void EnableMfa(string userId, string method);

        void ClearMfaCode(string userId);

        bool IsForgotPasswordRateLimited(string ipAddress);

        void RecordForgotPasswordAttempt(string ipAddress, string email, int? organizationId);

        string MaskEmail(string email);

        bool IsLoginIpRateLimited(string ipAddress);

        bool IsAccountLocked(string username, int? organizationId);

        DateTime? GetLockoutEndTimeUtc(string username, int? organizationId);

        int GetRemainingLoginAttempts(string username, int? organizationId);

        void RecordLoginAttempt(string username, string ipAddress, bool wasSuccessful, int? organizationId, string failureReason);

        void ClearFailedLoginAttempts(string username, int? organizationId);

        void ClearFailedLoginAttemptsForUser(string userId);

        void ClearAllLoginLockouts();

        bool IsEmailVerificationRequired();

        bool UserNeedsEmailVerification(string userId);

        bool SendEmailVerificationCode(string userId);

        bool ValidateEmailVerificationCode(string userId, string code);

        void MarkEmailVerified(string userId);

        bool SendVerificationCodeToAddress(string email, out string code);

        void RotateUserAccessToken(string userId);

        /// <summary>
        /// Rotates access tokens for all active users, forcing re-authentication on the next request.
        /// When organizationId is set, only users in that organization are affected.
        /// </summary>
        int InvalidateAllActiveSessions(int? organizationId);
    }
}
