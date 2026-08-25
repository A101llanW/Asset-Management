using System;
using System.Diagnostics;
using System.IO;
using System.Web;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;

namespace AssetManagement.Infrastructure.Security
{
    /// <summary>
    /// Development diagnostics for one-time security codes (MFA, password reset) when SMTP is not configured.
    /// Output appears in the IDE Output window (Visual Studio / Cursor), the hosting terminal, and App_Data/dev-security-output.log.
    /// </summary>
    public static class SecurityDiagnostics
    {
        public static void LogOneTimeCode(string purpose, string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                return;
            }

            WriteOutput(string.Format(
                "[{0}] {1} code for {2}: {3}",
                KenyaTimeHelper.FormatLogTimestamp(DateTime.UtcNow),
                purpose,
                email,
                code));
        }

        public static void LogPasswordResetLink(string email, string resetLink)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(resetLink))
            {
                return;
            }

            WriteOutput(string.Format(
                "[{0}] PASSWORD RESET link for {1}: {2}",
                KenyaTimeHelper.FormatLogTimestamp(DateTime.UtcNow),
                email,
                resetLink));
        }

        public static void LogInvitationLink(string email, string inviteLink)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(inviteLink))
            {
                return;
            }

            WriteOutput(string.Format(
                "[{0}] USER INVITATION link for {1}: {2}",
                KenyaTimeHelper.FormatLogTimestamp(DateTime.UtcNow),
                email,
                inviteLink));
        }

        public static void LogMfaDevBypass(string userId)
        {
            WriteOutput(string.Format(
                "[{0}] MFA dev bypass: any code accepted for user {1} (MfaAllowAnyCode=true; disable for production).",
                KenyaTimeHelper.FormatLogTimestamp(DateTime.UtcNow),
                userId ?? "(unknown)"));
        }

        public static string GetDevLogFilePath()
        {
            return ResolveDevLogPath();
        }

        private static void WriteOutput(string message)
        {
            Trace.WriteLine(message);
            Debug.WriteLine(message);
            Console.WriteLine(message);

            if (!DeploymentSecuritySettings.MfaAllowAnyCode)
            {
                return;
            }

            try
            {
                var logPath = ResolveDevLogPath();
                var logDirectory = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrWhiteSpace(logDirectory) && !Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                File.AppendAllText(logPath, message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private static string ResolveDevLogPath()
        {
            try
            {
                if (HttpContext.Current != null)
                {
                    return HttpContext.Current.Server.MapPath("~/App_Data/dev-security-output.log");
                }
            }
            catch
            {
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "dev-security-output.log");
        }
    }
}
