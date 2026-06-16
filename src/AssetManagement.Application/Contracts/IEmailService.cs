namespace AssetManagement.Application.Contracts
{
    public interface IEmailService
    {
        bool IsConfigured { get; }

        void SendPasswordResetEmail(string to, string resetLink);

        void SendMfaCodeEmail(string to, string code);
    }
}
