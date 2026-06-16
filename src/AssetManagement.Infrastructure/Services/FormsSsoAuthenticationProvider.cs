using AssetManagement.Application.Contracts;

namespace AssetManagement.Infrastructure.Services
{
    public class FormsSsoAuthenticationProvider : ISsoAuthenticationProvider
    {
        public bool IsEnabled
        {
            get { return false; }
        }

        public SsoAuthenticationResult TryAuthenticate(string externalToken)
        {
            return new SsoAuthenticationResult
            {
                IsConfigured = false,
                Succeeded = false,
                Message = "SSO is not configured. Use local account credentials or enable an external identity provider."
            };
        }
    }
}
