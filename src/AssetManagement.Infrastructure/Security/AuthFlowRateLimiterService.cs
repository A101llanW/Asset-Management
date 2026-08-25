using System;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Security
{
    public class AuthFlowRateLimiterService : IAuthFlowRateLimiter
    {
        private const int MaxMfaSendsPerWindow = 3;
        private static readonly TimeSpan MfaSendWindow = TimeSpan.FromMinutes(15);

        private const int MaxMfaVerifyFailures = 5;
        private static readonly TimeSpan MfaVerifyFailureWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MfaVerifyLockoutDuration = TimeSpan.FromMinutes(30);

        private const int MaxRegistrationsPerWindow = 5;
        private static readonly TimeSpan RegistrationWindow = TimeSpan.FromHours(1);

        private const int MaxInviteAcceptsPerWindow = 5;
        private static readonly TimeSpan InviteAcceptWindow = TimeSpan.FromHours(1);

        private const int MaxResetPasswordSubmitsPerWindow = 10;
        private static readonly TimeSpan ResetPasswordSubmitWindow = TimeSpan.FromHours(1);

        private const int MaxResetPasswordFailures = 5;
        private static readonly TimeSpan ResetPasswordFailureWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResetPasswordLockoutDuration = TimeSpan.FromMinutes(30);

        private readonly AuthFlowRateLimitRepository _repository;

        public AuthFlowRateLimiterService(ISqlConnectionFactory connectionFactory)
        {
            _repository = new AuthFlowRateLimitRepository(connectionFactory);
        }

        public bool TryAcquireMfaSend(string userId)
        {
            return TryAcquireCounter(BuildUserScopeKey("mfa-send", userId), MaxMfaSendsPerWindow, MfaSendWindow);
        }

        public bool IsMfaVerifyAllowed(string userId, out int minutesRemaining)
        {
            return IsFailureLockoutExpired(BuildUserScopeKey("mfa-verify", userId), out minutesRemaining);
        }

        public void RecordMfaVerifyFailure(string userId)
        {
            RecordFailureLockout(
                BuildUserScopeKey("mfa-verify", userId),
                MaxMfaVerifyFailures,
                MfaVerifyFailureWindow,
                MfaVerifyLockoutDuration);
        }

        public void ClearMfaVerifyFailures(string userId)
        {
            ClearFailureLockout(BuildUserScopeKey("mfa-verify", userId));
        }

        public bool TryAcquireRegistration(string tenantToken, string clientAddress)
        {
            return TryAcquireCounter(BuildRegistrationKey(tenantToken, clientAddress), MaxRegistrationsPerWindow, RegistrationWindow);
        }

        public bool TryAcquireInviteAccept(string tenantToken, string clientAddress)
        {
            return TryAcquireCounter(BuildInviteAcceptKey(tenantToken, clientAddress), MaxInviteAcceptsPerWindow, InviteAcceptWindow);
        }

        public bool TryAcquireResetPasswordSubmit(string clientAddress)
        {
            return TryAcquireCounter(BuildResetSubmitKey(clientAddress), MaxResetPasswordSubmitsPerWindow, ResetPasswordSubmitWindow);
        }

        public bool IsResetPasswordAllowed(string email, string code, out int minutesRemaining)
        {
            return IsFailureLockoutExpired(BuildResetTokenKey(email, code), out minutesRemaining);
        }

        public void RecordResetPasswordFailure(string email, string code)
        {
            RecordFailureLockout(
                BuildResetTokenKey(email, code),
                MaxResetPasswordFailures,
                ResetPasswordFailureWindow,
                ResetPasswordLockoutDuration);
        }

        public void ClearResetPasswordFailures(string email, string code)
        {
            ClearFailureLockout(BuildResetTokenKey(email, code));
        }

        private bool TryAcquireCounter(string key, int maxRequests, TimeSpan window)
        {
            try
            {
                return _repository.TryAcquireCounter(key, maxRequests, window);
            }
            catch (Exception ex)
            {
                LogPersistenceFallback(ex, key);
                return false;
            }
        }

        private bool IsFailureLockoutExpired(string key, out int minutesRemaining)
        {
            minutesRemaining = 0;
            try
            {
                return _repository.IsLockoutActive(key, out minutesRemaining);
            }
            catch (Exception ex)
            {
                LogPersistenceFallback(ex, key);
                minutesRemaining = 1;
                return false;
            }
        }

        private void RecordFailureLockout(string key, int maxFailures, TimeSpan failureWindow, TimeSpan lockoutDuration)
        {
            try
            {
                _repository.RecordFailure(key, maxFailures, failureWindow, lockoutDuration);
            }
            catch (Exception ex)
            {
                LogPersistenceFallback(ex, key);
                // Fail closed. Authentication limits must not become node-local during a database outage.
            }
        }

        private void ClearFailureLockout(string key)
        {
            try
            {
                _repository.ClearFailures(key);
            }
            catch (Exception ex)
            {
                LogPersistenceFallback(ex, key);
                // There is no local state to clear.
            }
        }

        private static void LogPersistenceFallback(Exception ex, string scopeKey)
        {
            System.Diagnostics.Trace.WriteLine(
                "AuthFlowRateLimiter persistence unavailable for scope '" + (scopeKey ?? string.Empty) + "': " + ex.Message);
        }

        private static string BuildUserScopeKey(string scope, string userId)
        {
            var normalizedUserId = (userId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedUserId))
            {
                return null;
            }

            return scope + "|" + normalizedUserId;
        }

        private static string BuildRegistrationKey(string tenantToken, string clientAddress)
        {
            var tenant = (tenantToken ?? string.Empty).Trim();
            var address = NormalizeClientAddress(clientAddress);
            return string.IsNullOrWhiteSpace(tenant)
                ? "register|" + address
                : "register|" + tenant + "|" + address;
        }

        private static string BuildInviteAcceptKey(string tenantToken, string clientAddress)
        {
            var tenant = (tenantToken ?? string.Empty).Trim();
            var address = NormalizeClientAddress(clientAddress);
            return string.IsNullOrWhiteSpace(tenant)
                ? "invite-accept|" + address
                : "invite-accept|" + tenant + "|" + address;
        }

        private static string BuildResetSubmitKey(string clientAddress)
        {
            return "reset-submit|" + NormalizeClientAddress(clientAddress);
        }

        private static string BuildResetTokenKey(string email, string code)
        {
            var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
            var normalizedCode = (code ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail) && string.IsNullOrWhiteSpace(normalizedCode))
            {
                return null;
            }

            return "reset-fail|" + normalizedEmail + "|" + normalizedCode;
        }

        private static string NormalizeClientAddress(string clientAddress)
        {
            return string.IsNullOrWhiteSpace(clientAddress) ? "unknown" : clientAddress.Trim();
        }

    }
}
