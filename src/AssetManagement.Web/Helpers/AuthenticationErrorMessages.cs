using System;
using System.Configuration;
using System.Web.Mvc;
using AssetManagement.Application.Helpers;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Web.Helpers
{
    public static class AuthenticationErrorMessages
    {
        public const string GenericLoginFailure = "Invalid email or password.";
        public const string GenericVerificationFailure = "Verification failed. Please try again.";
        public const string GenericMfaSendFailure = "Unable to send a verification code at this time.";
        public const string GenericMfaVerifyLockout = "Too many failed attempts. Please try again later.";
        public const string GenericRegistrationFailure = "Unable to complete registration at this time. Please try again later.";
        public const string GenericResetPasswordFailure = "Unable to reset your password. Please try again or request a new reset link.";
        public const string GenericChangePasswordFailure = "Unable to change your password. Please verify your current password and try again.";
        public const string GenericMfaSendSuccess = "Verification code sent.";

        public static bool IsGenericAuthMessagesEnabled()
        {
            var setting = ConfigurationManager.AppSettings["GenericAuthMessagesEnabled"];
            if (string.IsNullOrWhiteSpace(setting))
            {
                return false;
            }

            return string.Equals(setting.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(setting.Trim(), "1", StringComparison.OrdinalIgnoreCase);
        }

        public static string LoginIpRateLimited()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericLoginFailure
                : "Too many failed login attempts from your location. Please wait 15 minutes before trying again.";
        }

        public static string LoginAccountLocked(int minutesRemaining)
        {
            if (IsGenericAuthMessagesEnabled())
            {
                return GenericLoginFailure;
            }

            return "Account is locked. Please try again in " + minutesRemaining + " minutes.";
        }

        public static string LoginFailure(string email, string tenantSlug, int remainingAttempts)
        {
            if (IsGenericAuthMessagesEnabled())
            {
                return GenericLoginFailure;
            }

            return BuildDetailedLoginFailureMessage(email, tenantSlug, remainingAttempts);
        }

        public static string MfaInvalidCode()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericVerificationFailure
                : "Invalid or expired verification code.";
        }

        public static string MfaSetupInvalidCode()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericVerificationFailure
                : "Invalid verification code. Please try again.";
        }

        public static string MfaVerifyLockout(int minutesRemaining)
        {
            if (IsGenericAuthMessagesEnabled())
            {
                return GenericMfaVerifyLockout;
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Too many failed verification attempts. Please wait {0} minute(s) before trying again.",
                minutesRemaining);
        }

        public static string MfaSendRateLimited()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericMfaSendFailure
                : "Too many verification code requests. Please wait before requesting another code.";
        }

        public static string MfaSendFailure()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericMfaSendFailure
                : "Could not send a verification code. Contact your administrator or check Platform Settings → Email (SMTP).";
        }

        public static string MfaSendSuccess(bool devModeRelaxed)
        {
            if (IsGenericAuthMessagesEnabled())
            {
                return GenericMfaSendSuccess;
            }

            return devModeRelaxed
                ? "Verification code sent. In development, check debug/trace output."
                : "Verification code sent.";
        }

        public static string MfaResendSuccess(bool devModeRelaxed)
        {
            if (IsGenericAuthMessagesEnabled())
            {
                return GenericMfaSendSuccess;
            }

            return devModeRelaxed
                ? "Verification code resent. In development, check debug/trace output."
                : "Verification code resent.";
        }

        public static string MfaSendServiceFailure()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericMfaSendFailure
                : "Could not send verification code. Email delivery may not be configured.";
        }

        public static string MfaResendServiceFailure()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericMfaSendFailure
                : "Could not resend verification code. Email delivery may not be configured.";
        }

        public static string RegistrationRateLimited()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericRegistrationFailure
                : "Too many registration attempts from your location. Please try again later.";
        }

        public static string RegistrationFailure()
        {
            return GenericRegistrationFailure;
        }

        public static string InviteAcceptRateLimited()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericRegistrationFailure
                : "Too many invitation acceptance attempts from your location. Please try again later.";
        }

        public static string InviteAcceptInvalidToken()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericRegistrationFailure
                : "This invitation link is invalid or has expired.";
        }

        public static string InviteAcceptFailure()
        {
            return GenericRegistrationFailure;
        }

        public static string ResetPasswordRateLimited()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericResetPasswordFailure
                : "Too many password reset attempts from your location. Please try again later.";
        }

        public static string ResetPasswordTokenLockout(int minutesRemaining)
        {
            if (IsGenericAuthMessagesEnabled())
            {
                return GenericResetPasswordFailure;
            }

            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "Too many failed reset attempts for this link. Please wait {0} minute(s) or request a new reset link.",
                minutesRemaining);
        }

        public static string ResetPasswordFailure()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericResetPasswordFailure
                : "Password reset failed. Ensure the password meets complexity requirements.";
        }

        public static string ResetPasswordInvalidToken()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericResetPasswordFailure
                : "This reset link is invalid or has expired. Request a new reset link and try again.";
        }

        public static string ForgotPasswordSuccess(bool devModeRelaxed)
        {
            if (IsGenericAuthMessagesEnabled())
            {
                return "If that email is registered, a reset link has been sent.";
            }

            return devModeRelaxed
                ? "If that email is registered, a reset link has been sent. In development, check the IDE Output window (Debug) or terminal for the link."
                : "If that email is registered, a reset link has been sent.";
        }

        public static string ChangePasswordFailure()
        {
            return IsGenericAuthMessagesEnabled()
                ? GenericChangePasswordFailure
                : "Current password is incorrect or the new password could not be saved.";
        }

        private static string BuildDetailedLoginFailureMessage(string email, string tenantSlug, int remainingAttempts)
        {
            var message = BuildInvalidLoginMessage(remainingAttempts);
            if (!string.IsNullOrWhiteSpace(tenantSlug) || string.IsNullOrWhiteSpace(email))
            {
                return message;
            }

            if (!DemoLoginEmailHelper.IsPlatformAdminEmail(email))
            {
                return message;
            }

            var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return message;
            }

            var users = new UserAccountRepository(connectionFactory);
            if (users.FindPlatformAdminByEmail(email.Trim()) != null)
            {
                return message + " Check that the password is P@ssw0rd! for demo accounts.";
            }

            return message
                + " No platform administrator account exists yet. Run .\\tools\\database\\Unlock-Logins.ps1 (or .\\tools\\database\\Initialize-Database.ps1), then use superadmin@asset.local / P@ssw0rd! here."
                + " Company admins must use their organization portal (for example /nanosoft/Account/Login with nanosoft@asset.local).";
        }

        private static string BuildInvalidLoginMessage(int remainingAttempts)
        {
            if (remainingAttempts > 1)
            {
                return "Invalid login attempt. " + remainingAttempts + " attempts remaining.";
            }

            if (remainingAttempts == 1)
            {
                return "Invalid login attempt. 1 attempt remaining before account lockout.";
            }

            return "Invalid login attempt. Account is now locked for 30 minutes.";
        }
    }
}
