using AssetManagement.Application.Security;

namespace AssetManagement.Application.Contracts
{
    public interface IEmailService
    {
        bool IsConfigured { get; }

        EmailConfigurationStatus GetConfigurationStatus();

        void SendPasswordResetEmail(string to, string resetLink);

        void SendMfaCodeEmail(string to, string code);

        bool SendTestEmail(string to, out string errorMessage);
    }
}
