using System;
using System.Collections.Generic;
using System.Web;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Security
{
    public static class AuthFlowRateLimiter
    {
        public const string MfaSendLimitMessage = "Too many verification code requests. Please wait before requesting another code.";
        public const string MfaVerifyLockoutMessage = "Too many failed verification attempts. Please wait {0} minute(s) before trying again.";
        public const string RegistrationLimitMessage = "Too many registration attempts from your location. Please try again later.";
        public const string ResetPasswordSubmitLimitMessage = "Too many password reset attempts from your location. Please try again later.";
        public const string ResetPasswordFailureLockoutMessage = "Too many failed reset attempts for this link. Please wait {0} minute(s) or request a new reset link.";

        private const int MaxMfaSendsPerWindow = 3;
        private static readonly TimeSpan MfaSendWindow = TimeSpan.FromMinutes(15);

        private const int MaxMfaVerifyFailures = 5;
        private static readonly TimeSpan MfaVerifyFailureWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan MfaVerifyLockoutDuration = TimeSpan.FromMinutes(30);

        private const int MaxRegistrationsPerWindow = 5;
        private static readonly TimeSpan RegistrationWindow = TimeSpan.FromHours(1);

        private const int MaxResetPasswordSubmitsPerWindow = 10;
        private static readonly TimeSpan ResetPasswordSubmitWindow = TimeSpan.FromHours(1);

        private const int MaxResetPasswordFailures = 5;
        private static readonly TimeSpan ResetPasswordFailureWindow = TimeSpan.FromMinutes(15);
        private static readonly TimeSpan ResetPasswordLockoutDuration = TimeSpan.FromMinutes(30);

        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<string, CounterBucket> CounterBuckets = new Dictionary<string, CounterBucket>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, FailureLockoutState> FailureLockouts = new Dictionary<string, FailureLockoutState>(StringComparer.OrdinalIgnoreCase);

        public static bool TryAcquireMfaSend(string userId)
        {
            return TryAcquireCounter(BuildUserScopeKey("mfa-send", userId), MaxMfaSendsPerWindow, MfaSendWindow);
        }

        public static bool IsMfaVerifyAllowed(string userId, out int minutesRemaining)
        {
            return IsFailureLockoutExpired(BuildUserScopeKey("mfa-verify", userId), out minutesRemaining);
        }

        public static void RecordMfaVerifyFailure(string userId)
        {
            RecordFailureLockout(
                BuildUserScopeKey("mfa-verify", userId),
                MaxMfaVerifyFailures,
                MfaVerifyFailureWindow,
                MfaVerifyLockoutDuration);
        }

        public static void ClearMfaVerifyFailures(string userId)
        {
            ClearFailureLockout(BuildUserScopeKey("mfa-verify", userId));
        }

        public static bool TryAcquireRegistration(HttpContextBase context)
        {
            return TryAcquireCounter(BuildRegistrationKey(context), MaxRegistrationsPerWindow, RegistrationWindow);
        }

        public static bool TryAcquireResetPasswordSubmit(HttpContextBase context)
        {
            return TryAcquireCounter(BuildResetSubmitKey(context), MaxResetPasswordSubmitsPerWindow, ResetPasswordSubmitWindow);
        }

        public static bool IsResetPasswordAllowed(string email, string code, out int minutesRemaining)
        {
            return IsFailureLockoutExpired(BuildResetTokenKey(email, code), out minutesRemaining);
        }

        public static void RecordResetPasswordFailure(string email, string code)
        {
            RecordFailureLockout(
                BuildResetTokenKey(email, code),
                MaxResetPasswordFailures,
                ResetPasswordFailureWindow,
                ResetPasswordLockoutDuration);
        }

        public static void ClearResetPasswordFailures(string email, string code)
        {
            ClearFailureLockout(BuildResetTokenKey(email, code));
        }

        private static bool TryAcquireCounter(string key, int maxRequests, TimeSpan window)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            var now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                CounterBucket bucket;
                if (!CounterBuckets.TryGetValue(key, out bucket) || now - bucket.WindowStart >= window)
                {
                    bucket = new CounterBucket { WindowStart = now, Count = 0 };
                    CounterBuckets[key] = bucket;
                }

                if (bucket.Count >= maxRequests)
                {
                    return false;
                }

                bucket.Count++;
                return true;
            }
        }

        private static bool IsFailureLockoutExpired(string key, out int minutesRemaining)
        {
            minutesRemaining = 0;
            if (string.IsNullOrWhiteSpace(key))
            {
                return true;
            }

            var now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                FailureLockoutState state;
                if (!FailureLockouts.TryGetValue(key, out state))
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
                    FailureLockouts.Remove(key);
                }

                return true;
            }
        }

        private static void RecordFailureLockout(string key, int maxFailures, TimeSpan failureWindow, TimeSpan lockoutDuration)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            var now = DateTime.UtcNow;
            lock (SyncRoot)
            {
                FailureLockoutState state;
                if (!FailureLockouts.TryGetValue(key, out state))
                {
                    state = new FailureLockoutState();
                    FailureLockouts[key] = state;
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

        private static void ClearFailureLockout(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            lock (SyncRoot)
            {
                FailureLockouts.Remove(key);
            }
        }

        private static void PruneFailures(FailureLockoutState state, DateTime now, TimeSpan? failureWindow = null)
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

        private static string BuildRegistrationKey(HttpContextBase context)
        {
            var tenant = context == null ? null : TenantUrlHelper.GetTenantToken(context);
            var address = ResolveClientAddress(context);
            return string.IsNullOrWhiteSpace(tenant)
                ? "register|" + address
                : "register|" + tenant + "|" + address;
        }

        private static string BuildResetSubmitKey(HttpContextBase context)
        {
            return "reset-submit|" + ResolveClientAddress(context);
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

        private static string ResolveClientAddress(HttpContextBase context)
        {
            if (context == null || context.Request == null)
            {
                return "unknown";
            }

            var forwarded = context.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',')[0];
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first.Trim();
                }
            }

            return context.Request.UserHostAddress ?? "unknown";
        }

        private sealed class CounterBucket
        {
            public DateTime WindowStart { get; set; }

            public int Count { get; set; }
        }

        private sealed class FailureLockoutState
        {
            public FailureLockoutState()
            {
                Failures = new List<DateTime>();
            }

            public List<DateTime> Failures { get; private set; }

            public DateTime? LockedUntilUtc { get; set; }
        }
    }
}
