using AssetManagement.Application.Contracts;

namespace AssetManagement.Runner
{
    internal sealed class BootstrapCurrentUserContext : ICurrentUserContext
    {
        public BootstrapCurrentUserContext(string userId = "bootstrap")
        {
            UserId = userId;
        }

        public string UserId { get; private set; }

        public string UserName
        {
            get { return "bootstrap"; }
        }

        public string IPAddress
        {
            get { return "127.0.0.1"; }
        }
    }
}
