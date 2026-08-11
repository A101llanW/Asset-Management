using System;
using System.Configuration;
using System.Web;
using System.Web.Security;
using AssetManagement.Application.Security;

namespace AssetManagement.Web.Security
{
    /// <summary>
    /// Consistent Forms Authentication sign-out: expire auth cookie and abandon server session state.
    /// </summary>
    public static class AuthSessionHelper
    {
        public static void SignOut(HttpContextBase context, bool abandonSession = true)
        {
            if (context == null)
            {
                return;
            }

            FormsAuthentication.SignOut();
            ExpireCookie(context, FormsAuthentication.FormsCookieName);

            if (!abandonSession || context.Session == null)
            {
                return;
            }

            context.Session.Clear();
            context.Session.Abandon();
        }

        public static void ExpireCookie(HttpContextBase context, string name)
        {
            if (context == null || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var expiredCookie = new HttpCookie(name, string.Empty)
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Secure = RequiresSecureCookie(context)
            };

            context.Response.Cookies.Add(expiredCookie);
        }

        private static bool RequiresSecureCookie(HttpContextBase context)
        {
            if (context != null && context.Request != null && context.Request.IsSecureConnection)
            {
                return true;
            }

            return DeploymentSecuritySettings.RequireSecureCookies
                || string.Equals(
                    ConfigurationManager.AppSettings["RequireSecureCookies"],
                    "true",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
