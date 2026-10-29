using System;
using System.Configuration;
using System.Web;
using System.Web.Security;
using AssetManagement.Application.Contracts.Security;
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
            ExpireCookie(context, SessionCookieName());
        }

        /// <summary>
        /// Log off (QA 65d06e1: a cookie copied before Log off still worked). Every auth ticket carries the user's
        /// Users.AccessToken and Global.asax checks it against the database on each request, so rotating it here makes
        /// every earlier auth cookie of this user fail on its next request: server side, so it holds across app restarts
        /// and IIS worker recycles. Scope: all sessions of the user who logs off (same mechanism as password / email
        /// change); other users are not touched. No schema change. Returns false (and never throws) if it could not rotate,
        /// so Log off still clears the cookies.
        /// </summary>
        public static bool RevokeUserAccessToken(IAccountSecurityService accountSecurity, string userId)
        {
            if (accountSecurity == null || string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            try
            {
                accountSecurity.RotateUserAccessToken(userId);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Log off: access token rotation failed for user " + userId + ": " + ex.Message);
                return false;
            }
        }

        public static string SessionCookieName()
        {
            try
            {
                var section = ConfigurationManager.GetSection("system.web/sessionState") as System.Web.Configuration.SessionStateSection;
                if (section != null && !string.IsNullOrWhiteSpace(section.CookieName))
                {
                    return section.CookieName;
                }
            }
            catch (Exception)
            {
                // Fall back to the ASP.NET default.
            }

            return "ASP.NET_SessionId";
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
