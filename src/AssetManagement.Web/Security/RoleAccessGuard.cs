using System;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Security
{
    /// <summary>
    /// Role Active flag (Roles.IsActive). A user whose tenant role is inactive cannot sign in (checked right after
    /// the password, before MFA, and again before the auth cookie is issued) and is signed out on the next request.
    /// Platform users and platform roles (Platform Admin) are never blocked. The UI only shows the generic
    /// "Access denied." message; the reason is written to SecurityEvents.
    /// No cache: one primary-key lookup per authenticated request, so a deactivation takes effect immediately.
    /// </summary>
    public static class RoleAccessGuard
    {
        public const string GenericMessage = "Access denied.";
        public const string AccessDeniedReason = "access_denied";
        public const string LoginDeniedEventType = "AUTH_DENIED_ROLE_INACTIVE";
        public const string SessionEndedEventType = "AUTH_SESSION_ENDED_ROLE_INACTIVE";

        public static bool IsBlocked(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return false;
            }

            return new UserAccountRepository(connectionFactory).IsTenantUserRoleInactive(userId);
        }

        public static void RecordDenied(string eventType, string email, string ipAddress, int? organizationId)
        {
            try
            {
                var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
                if (connectionFactory == null)
                {
                    return;
                }

                new SecurityEventRepository(connectionFactory).Record(eventType, email, ipAddress, organizationId);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("RoleAccessGuard: could not record security event " + eventType + ": " + ex.Message);
            }
        }

        public static string BuildAccessDeniedLoginUrl(string tenantSlug)
        {
            var redirect = TenantUrlHelper.CreateTenantLoginRedirect(tenantSlug) as RedirectResult;
            var url = redirect != null && !string.IsNullOrWhiteSpace(redirect.Url)
                ? redirect.Url
                : AppPathHelper.ToAppAbsolute("~/Account/Login");
            return url + (url.Contains("?") ? "&" : "?") + "reason=" + AccessDeniedReason;
        }

        public static string BuildAccessDeniedLoginUrl(int? organizationId)
        {
            string slug = null;
            if (organizationId.HasValue)
            {
                try
                {
                    var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
                    if (connectionFactory != null)
                    {
                        slug = new UserAccountRepository(connectionFactory).FindOrganizationSlugById(organizationId.Value);
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("RoleAccessGuard: could not resolve tenant slug: " + ex.Message);
                }
            }

            return BuildAccessDeniedLoginUrl(slug);
        }

        public static bool IsAccessDeniedReason(HttpRequestBase request)
        {
            return request != null
                && string.Equals(request.QueryString["reason"], AccessDeniedReason, StringComparison.OrdinalIgnoreCase);
        }
    }
}
