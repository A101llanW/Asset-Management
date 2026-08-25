using System.Security.Principal;
using System.Web.Security;

namespace AssetManagement.Infrastructure.Security
{
    public static class FormsAuthHelper
    {
        public static string GetUserId(IPrincipal principal)
        {
            if (principal == null || principal.Identity == null || !principal.Identity.IsAuthenticated)
            {
                return null;
            }

            var formsIdentity = principal.Identity as FormsIdentity;
            if (formsIdentity != null && !string.IsNullOrWhiteSpace(formsIdentity.Ticket.UserData))
            {
                return ExtractUserIdFromTicketData(formsIdentity.Ticket.UserData);
            }

            return null;
        }

        private static string ExtractUserIdFromTicketData(string userData)
        {
            if (string.IsNullOrWhiteSpace(userData))
            {
                return null;
            }

            var pipeIndex = userData.IndexOf('|');
            return pipeIndex < 0 ? userData : userData.Substring(0, pipeIndex);
        }
    }
}
