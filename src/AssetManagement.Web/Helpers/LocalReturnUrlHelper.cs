using System;
using System.Web.Mvc;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Parses and formats local return URLs for safe redirects after login and navigation.
    /// </summary>
    public static class LocalReturnUrlHelper
    {
        public static bool TryParseLocalReturnUri(string returnPath, UrlHelper urlHelper, out Uri parsedUri)
        {
            parsedUri = null;
            if (string.IsNullOrWhiteSpace(returnPath) || urlHelper == null || !urlHelper.IsLocalUrl(returnPath))
            {
                return false;
            }

            if (returnPath.StartsWith("//", StringComparison.Ordinal) || returnPath.StartsWith(@"/\", StringComparison.Ordinal))
            {
                return false;
            }

            return Uri.TryCreate("https://local.test" + returnPath, UriKind.Absolute, out parsedUri);
        }

        public static string FormatReturnPathAndQuery(Uri returnUri)
        {
            if (returnUri == null)
            {
                return null;
            }

            return returnUri.PathAndQuery;
        }

        /// <summary>
        /// Tenant root and default dashboard routes require Reports.View; treat them as non-return targets
        /// so post-login redirect uses the user's first permitted destination instead.
        /// </summary>
        public static bool IsDefaultTenantLandingPath(string returnPath)
        {
            if (string.IsNullOrWhiteSpace(returnPath))
            {
                return false;
            }

            var path = returnPath.Split('?')[0].Trim();
            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            var segments = path.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0 || segments.Length == 1)
            {
                return segments.Length == 1;
            }

            if (segments.Length == 2 &&
                string.Equals(segments[1], "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (segments.Length == 3 &&
                string.Equals(segments[1], "Dashboard", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(segments[2], "Index", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
