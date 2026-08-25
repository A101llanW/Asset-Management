using System;
using System.Configuration;
using System.Reflection;
using System.Web;
using System.Web.Security;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Security
{
    public static class CurrentUserExtensions
    {
        public static string GetUserId(this System.Security.Principal.IPrincipal principal)
        {
            return Infrastructure.Security.FormsAuthHelper.GetUserId(principal);
        }

        public static void SetAuthCookie(HttpResponseBase response, ApplicationUser user, bool rememberMe)
        {
            var userAgent = HttpContext.Current != null && HttpContext.Current.Request != null
                ? HttpContext.Current.Request.UserAgent
                : null;
            SetAuthCookie(response, user, rememberMe, userAgent);
        }

        public static void SetAuthCookie(HttpResponseBase response, ApplicationUser user, bool rememberMe, string userAgent)
        {
            SetAuthCookie(response, user, rememberMe, userAgent, user != null && user.RequirePasswordChange);
        }

        public static void SetAuthCookie(
            HttpResponseBase response,
            ApplicationUser user,
            bool rememberMe,
            string userAgent,
            bool requirePasswordChange)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Id))
            {
                return;
            }

            var uaHash = UserAgentFingerprint.Compute(userAgent);
            var userData = AuthTicketHelper.BuildUserData(
                user.Id,
                user.OrganizationId,
                user.AccessToken,
                uaHash,
                requirePasswordChange);

            var ticket = new FormsAuthenticationTicket(
                1,
                string.IsNullOrWhiteSpace(user.Email) ? user.Id : user.Email,
                DateTime.Now,
                DateTime.Now.AddMinutes(FormsAuthentication.Timeout.TotalMinutes),
                rememberMe,
                userData,
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
