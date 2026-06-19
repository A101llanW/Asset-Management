using System;

namespace AssetManagement.Application.Contracts.Security
{
    public interface IAccountSecurityService
    {
        bool RequiresPrivilegedMfa(string userId);

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
    }
}
