using System;
using System.Web;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// QA 2026-10-29 (7b36ea8): after Log off, replaying the old cookie on /a46138179/Dashboard was sent to the ROOT
    /// login (/Account/Login?reason=session_invalid), losing the tenant. A session that ends server side (revoked
    /// token, fingerprint mismatch, locked account) now goes back to a TENANT login page:
    /// 1. the organisation the ticket belongs to (the user's own company), when it is known and has a valid slug;
    /// 2. otherwise the first path segment of the request, but only when it resolves to a known, active organisation
    ///    (validated by <paramref name="resolveKnownSlug"/>; unknown segments never reach the Location header);
    /// 3. otherwise the root login page.
    /// The ReturnUrl is carried only for GET/HEAD, only when it is a local path inside the same tenant, and never for
    /// Account pages or the default landing page, so this cannot become an open redirect.
    /// </summary>
    public static class SessionEndLoginUrl
    {
        public static string Build(
            string appRelativePath,
            string rawUrl,
            string httpMethod,
            string reason,
            Func<string, string> resolveKnownSlug,
            string ownOrganizationSlug,
            string rootLoginUrl)
        {
            var safeReason = SafeReason(reason);
            var requestSlug = ResolveRequestSlug(appRelativePath, resolveKnownSlug);
            var ownSlug = TenantUrlHelper.IsValidTenantSlug(ownOrganizationSlug) ? ownOrganizationSlug : null;
            var slug = ownSlug ?? requestSlug;

            if (slug == null)
            {
                var root = string.IsNullOrEmpty(rootLoginUrl) ? AppPathHelper.ToAppAbsolute("~/Account/Login") : rootLoginUrl;
                return root + (root.Contains("?") ? "&" : "?") + "reason=" + safeReason;
            }

            var url = TenantUrlHelper.BuildTenantPath(slug, "Account", "Login") + "?reason=" + safeReason;
            var sameTenant = requestSlug != null && string.Equals(requestSlug, slug, StringComparison.OrdinalIgnoreCase);
            var returnUrl = sameTenant ? SafeReturnUrl(rawUrl, httpMethod) : null;
            if (!string.IsNullOrEmpty(returnUrl))
            {
                url += "&ReturnUrl=" + HttpUtility.UrlEncode(returnUrl);
            }

            return url;
        }

        /// <summary>First segment of "~/seg/..." when it is shaped like a tenant token and resolves to a known organisation.</summary>
        public static string ResolveRequestSlug(string appRelativePath, Func<string, string> resolveKnownSlug)
        {
            var segment = FirstSegment(appRelativePath);
            if (segment == null || resolveKnownSlug == null || !TenantUrlHelper.IsValidTenantSlug(segment))
            {
                return null;
            }

            string known;
            try
            {
                known = resolveKnownSlug(segment);
            }
            catch (Exception)
            {
                return null;
            }

            return TenantUrlHelper.IsValidTenantSlug(known) ? known : null;
        }

        public static string FirstSegment(string appRelativePath)
        {
            if (string.IsNullOrWhiteSpace(appRelativePath))
            {
                return null;
            }

            var path = appRelativePath.Trim();
            if (path.StartsWith("~", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            var segments = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            return segments.Length == 0 ? null : segments[0];
        }

        private static string SafeReason(string reason)
        {
            switch ((reason ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "session_hijack":
                    return "session_hijack";
                case "account_locked":
                    return "account_locked";
                default:
                    return "session_invalid";
            }
        }

        private static string SafeReturnUrl(string rawUrl, string httpMethod)
        {
            var method = httpMethod ?? "GET";
            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(rawUrl)
                || !rawUrl.StartsWith("/", StringComparison.Ordinal)
                || rawUrl.StartsWith("//", StringComparison.Ordinal)
                || rawUrl.StartsWith("/\\", StringComparison.Ordinal)
                || rawUrl.IndexOf("://", StringComparison.Ordinal) >= 0
                || rawUrl.IndexOf("/account/", StringComparison.OrdinalIgnoreCase) >= 0
                || LocalReturnUrlHelper.IsDefaultTenantLandingPath(rawUrl))
            {
                return null;
            }

            return rawUrl;
        }
    }
}