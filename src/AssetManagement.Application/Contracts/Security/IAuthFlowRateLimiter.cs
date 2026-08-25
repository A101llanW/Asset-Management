using System;

namespace AssetManagement.Application.Contracts.Security
{
    public interface IAuthFlowRateLimiter
    {
        bool TryAcquireMfaSend(string userId);

        bool IsMfaVerifyAllowed(string userId, out int minutesRemaining);

        void RecordMfaVerifyFailure(string userId);

        void ClearMfaVerifyFailures(string userId);

        bool TryAcquireRegistration(string tenantToken, string clientAddress);

        bool TryAcquireInviteAccept(string tenantToken, string clientAddress);

        bool TryAcquireResetPasswordSubmit(string clientAddress);

        bool IsResetPasswordAllowed(string email, string code, out int minutesRemaining);

        void RecordResetPasswordFailure(string email, string code);

        void ClearResetPasswordFailures(string email, string code);
    }
}
