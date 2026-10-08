namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Platform Usage page: which AuditLog.Action values count as a company login.
    /// </summary>
    public static class PlatformUsageMetrics
    {
        /// <summary>Counted for 24h / 7d / 30d login columns.</summary>
        public const string LoginActionCounted = "HTTP.Account.Login";

        /// <summary>Shown in the Usage page footnote; MFA and consent noise is excluded from login totals.</summary>
        public const string LoginActionsExcludedNote =
            "Excluded from login counts (MFA / consent / account noise): HTTP.Account.VerifyMfa, HTTP.Account.SetupMfa, HTTP.Account.ResendMfaCode, HTTP.Account.SendSetupMfaCode, HTTP.Account.ConfirmLegalConsent, HTTP.Account.LogOff, HTTP.Account.ChangePassword, HTTP.Account.ForgotPassword, HTTP.Account.ResetPassword, HTTP.Account.AcceptInvite.";
    }
}