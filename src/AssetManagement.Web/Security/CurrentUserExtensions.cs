using System;
using System.Configuration;
using System.Reflection;
using System.Security.Principal;
using System.Web;
using System.Web.Security;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Security;

namespace AssetManagement.Web.Security
{
    public static class CurrentUserExtensions
    {
        public static string GetUserId(this IPrincipal principal)
        {
            return FormsAuthHelper.GetUserId(principal);
        }

        public static void SetAuthCookie(HttpResponseBase response, ApplicationUser user, bool rememberMe)
        {
            var ticket = new FormsAuthenticationTicket(
                1,
                user.Email,
                DateTime.Now,
                DateTime.Now.AddMinutes(FormsAuthentication.Timeout.TotalMinutes),
                rememberMe,
                user.Id,
                FormsAuthentication.FormsCookiePath);

            var encryptedTicket = FormsAuthentication.Encrypt(ticket);
            var cookie = new HttpCookie(FormsAuthentication.FormsCookieName, encryptedTicket)
            {
                HttpOnly = true,
                Secure = RequiresSecureCookie()
            };

            SetCookieSameSite(cookie, "Lax");

            if (rememberMe)
            {
                cookie.Expires = ticket.Expiration;
            }

            response.Cookies.Add(cookie);
        }

        private static bool RequiresSecureCookie()
        {
            var context = HttpContext.Current;
            if (context != null && context.Request.IsSecureConnection)
            {
                return true;
            }

            var setting = ConfigurationManager.AppSettings["RequireSecureCookies"];
            return string.Equals(setting, "true", StringComparison.OrdinalIgnoreCase);
        }

        private static void SetCookieSameSite(HttpCookie cookie, string mode)
        {
            try
            {
                var sameSiteModeType = Type.GetType(
                    "System.Web.SameSiteMode, System.Web, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                    false);
                if (sameSiteModeType == null)
                {
                    return;
                }

                var property = typeof(HttpCookie).GetProperty("SameSite", BindingFlags.Instance | BindingFlags.Public);
                if (property == null)
                {
                    return;
                }

                var enumValue = Enum.Parse(sameSiteModeType, mode, true);
                property.SetValue(cookie, enumValue, null);
            }
            catch
            {
                // SameSite not supported on this runtime.
            }
        }
    }
}
