namespace AssetManagement.Web.Security
{
    /// <summary>
    /// User-facing auth-flow rate limit messages (limits enforced by <see cref="AssetManagement.Infrastructure.Security.AuthFlowRateLimiterService"/>).
    /// </summary>
    public static class AuthFlowRateLimiter
    {
        public const string MfaSendLimitMessage = "Too many verification code requests. Please wait before requesting another code.";
        public const string MfaVerifyLockoutMessage = "Too many failed verification attempts. Please wait {0} minute(s) before trying again.";
        public const string RegistrationLimitMessage = "Too many registration attempts from your location. Please try again later.";
        public const string ResetPasswordSubmitLimitMessage = "Too many password reset attempts from your location. Please try again later.";
        public const string ResetPasswordFailureLockoutMessage = "Too many failed reset attempts for this link. Please wait {0} minute(s) or request a new reset link.";
    }
}
