using System;
using System.Collections.Generic;
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

        private static readonly object FallbackSyncRoot = new object();
        private static readonly Dictionary<string, FallbackCounterBucket> FallbackCounterBuckets =
            new Dictionary<string, FallbackCounterBucket>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, FallbackFailureLockoutState> FallbackFailureLockouts =
            new Dictionary<string, FallbackFailureLockoutState>(StringComparer.OrdinalIgnoreCase);

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
                return TryAcquireCounterFallback(key, maxRequests, window);
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
                return IsFailureLockoutExpiredFallback(key, out minutesRemaining);
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
                RecordFailureLockoutFallback(key, maxFailures, failureWindow, lockoutDuration);
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
                ClearFailureLockoutFallback(key);
            }
        }

        private static void LogPersistenceFallback(Exception ex, string scopeKey)
        {
            System.Diagnostics.Trace.WriteLine(
                "AuthFlowRateLimiter persistence unavailable for scope '" + (scopeKey ?? string.Empty) + "': " + ex.Message);
        }

        private static bool TryAcquireCounterFallback(string key, int maxRequests, TimeSpan window)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            var now = DateTime.UtcNow;
            lock (FallbackSyncRoot)
            {
                FallbackCounterBucket bucket;
                if (!FallbackCounterBuckets.TryGetValue(key, out bucket) || now - bucket.WindowStart >= window)
                {
                    bucket = new FallbackCounterBucket { WindowStart = now, Count = 0 };
                    FallbackCounterBuckets[key] = bucket;
                }

                if (bucket.Count >= maxRequests)
                {
                    return false;
                }

                bucket.Count++;
                return true;
            }
        }

        private static bool IsFailureLockoutExpiredFallback(string key, out int minutesRemaining)
        {
            minutesRemaining = 0;
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            var now = DateTime.UtcNow;
            lock (FallbackSyncRoot)
            {
                FallbackFailureLockoutState state;
                if (!FallbackFailureLockouts.TryGetValue(key, out state))
                {
                    return true;
                }

                PruneFailures(state, now);
                if (state.LockedUntilUtc.HasValue && state.LockedUntilUtc.Value > now)
                {
                    minutesRemaining = Math.Max(1, (int)Math.Ceiling((state.LockedUntilUtc.Value - now).TotalMinutes));
                    return false;
                }

                if (state.LockedUntilUtc.HasValue && state.LockedUntilUtc.Value <= now)
                {
                    FallbackFailureLockouts.Remove(key);
                }

                return true;
            }
        }

        private static void RecordFailureLockoutFallback(string key, int maxFailures, TimeSpan failureWindow, TimeSpan lockoutDuration)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var now = DateTime.UtcNow;
            lock (FallbackSyncRoot)
            {
                FallbackFailureLockoutState state;
                if (!FallbackFailureLockouts.TryGetValue(key, out state))
                {
                    state = new FallbackFailureLockoutState();
                    FallbackFailureLockouts[key] = state;
                }

                PruneFailures(state, now, failureWindow);
                state.Failures.Add(now);

                if (state.Failures.Count >= maxFailures)
                {
                    state.LockedUntilUtc = now.Add(lockoutDuration);
                    state.Failures.Clear();
                }
            }
        }

        private static void ClearFailureLockoutFallback(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (FallbackSyncRoot)
            {
                FallbackFailureLockouts.Remove(key);
            }
        }

        private static void PruneFailures(FallbackFailureLockoutState state, DateTime now, TimeSpan? failureWindow = null)
        {
            if (state == null || state.Failures == null || state.Failures.Count == 0)
            {
                return;
            }

            var window = failureWindow ?? MfaVerifyFailureWindow;
            var cutoff = now - window;
            for (var index = state.Failures.Count - 1; index >= 0; index--)
            {
                if (state.Failures[index] < cutoff)
                {
                    state.Failures.RemoveAt(index);
                }
            }
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

        private sealed class FallbackCounterBucket
        {
            public DateTime WindowStart { get; set; }

            public int Count { get; set; }
        }

        private sealed class FallbackFailureLockoutState
        {
            public FallbackFailureLockoutState()
            {
                Failures = new List<DateTime>();
            }

            public List<DateTime> Failures { get; private set; }

            public DateTime? LockedUntilUtc { get; set; }
        }
    }
}
