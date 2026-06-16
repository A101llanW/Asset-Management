namespace AssetManagement.Application.Contracts
{
    public class SsoAuthenticationResult
    {
        public bool IsConfigured { get; set; }

        public bool Succeeded { get; set; }

        public string UserId { get; set; }

        public string Message { get; set; }
    }

    public interface ISsoAuthenticationProvider
    {
        bool IsEnabled { get; }

        SsoAuthenticationResult TryAuthenticate(string externalToken);
    }
}
