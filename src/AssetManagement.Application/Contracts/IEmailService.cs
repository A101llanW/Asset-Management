using AssetManagement.Application.Email;
using AssetManagement.Application.Security;

namespace AssetManagement.Application.Contracts
{
    public interface IEmailService
    {
        bool IsConfigured { get; }

        /// <summary>True when this sender uses an organization's own SMTP rather than the platform default.</summary>
        bool UsesOrganizationSmtp { get; }

        /// <summary>"company SMTP host:port" or "platform default SMTP". Never contains credentials.</summary>
        string RouteDescription { get; }

        EmailConfigurationStatus GetConfigurationStatus();

        /// <summary>
        /// Sender for one organization's email: its own SMTP when enabled and complete, otherwise the platform
        /// default. Pass null for platform (superadmin) email. Never throws; lookup errors fall back to the default.
        /// </summary>
        IEmailService ForOrganization(int? organizationId);

        /// <summary>Sender bound to explicit settings (Settings &gt; Email "Send test email" before turning company SMTP on).</summary>
        IEmailService ForSettings(SmtpSettings settings);

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
