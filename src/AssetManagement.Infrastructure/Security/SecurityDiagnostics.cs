using System;
using System.Diagnostics;

namespace AssetManagement.Infrastructure.Security
{
    /// <summary>
    /// Development diagnostics for one-time security codes (MFA, password reset) when SMTP is not configured.
    /// </summary>
    public static class SecurityDiagnostics
    {
        public static void LogOneTimeCode(string purpose, string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            var message = string.Format(
                "[{0:u}] {1} code for {2}: {3}",
                DateTime.UtcNow,
                purpose,
                email,
                code);
            Trace.WriteLine(message);
            Debug.WriteLine(message);
        }

        public static void LogMfaDevBypass(string userId)
        {
            var message = string.Format(
                "[{0:u}] MFA dev bypass: any code accepted for user {1} (MfaAllowAnyCode=true; disable for production).",
                DateTime.UtcNow,
                userId ?? "(unknown)");
            Trace.WriteLine(message);
            Debug.WriteLine(message);
        }
    }
}
