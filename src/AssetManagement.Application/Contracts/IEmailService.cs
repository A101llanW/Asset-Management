using AssetManagement.Application.Security;

namespace AssetManagement.Application.Contracts
{
    public interface IEmailService
    {
        bool IsConfigured { get; }

        EmailConfigurationStatus GetConfigurationStatus();

        void SendPasswordResetEmail(string to, string resetLink);

        void SendUserInvitationEmail(string to, string inviteLink, string organizationName);

        void SendMfaCodeEmail(string to, string code);

        bool SendTestEmail(string to, out string errorMessage);

        /// <summary>
        /// Best-effort security alert. Returns false if SMTP is missing or send fails; never throws for soft-fail path.
        /// </summary>
        bool TrySendSecurityAlert(string to, string subject, string body);
    }
}
