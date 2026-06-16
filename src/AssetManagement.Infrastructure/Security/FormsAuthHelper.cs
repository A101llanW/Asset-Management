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
                return formsIdentity.Ticket.UserData;
            }

            return null;
        }
    }
}
