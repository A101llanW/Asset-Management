using AssetManagement.Application.Contracts;

namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Validates SMTP readiness at application startup when strict MFA is enabled.
    /// </summary>
    public static class SmtpStartupValidator
    {
        public static string ValidateForStartup(IEmailService emailService)
        {
            if (!DeploymentSecuritySettings.RequiresSmtpForAuthEmails)
            {
                return null;
            }

            if (emailService == null)
            {
                return "IEmailService is not registered. MFA and password reset email delivery is unavailable.";
            }

            var status = emailService.GetConfigurationStatus();
            if (status.IsReadyForAuthDelivery)
            {
                return null;
            }

            return "SMTP is required when MfaAllowAnyCode=false. Configure Platform Settings → Email or production Web.config. Missing: "
                + string.Join(", ", status.GetMissingRequirements())
                + ".";
        }
    }
}
