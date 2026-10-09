using System;
using System.Net;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Absolute base URL for links that leave the app (QR scan URLs, invite / reset e-mails, portal links).
    /// rel1009g: ONE rule shared with Web ExternalUrlHelper (see <see cref="ResolveAbsoluteBase"/>):
    ///   * configured ExternalBaseUrl, plus the IIS application path when the configured URL is host-only;
    ///   * but the request (authority + application path) when the configured host is loopback (localhost /
    ///     127.0.0.1 / ::1) and the request host is not, or both are loopback with a different port / scheme;
    ///   * the request base when nothing is configured.
    /// The org code is appended by the callers (/{org}/...).
    /// </summary>
    public static class AssetScanUrlHelper
    {
        /// <summary>Legacy two-string overload: the request base may already carry the application path.</summary>
        public static string ResolveBaseUrl(string configuredExternalBaseUrl, string requestAuthorityBaseUrl)
        {
            Uri requestUri = null;
            if (!string.IsNullOrWhiteSpace(requestAuthorityBaseUrl))
            {
                Uri.TryCreate(requestAuthorityBaseUrl.Trim(), UriKind.Absolute, out requestUri);
            }

            if (requestUri == null)
            {
                return string.IsNullOrWhiteSpace(configuredExternalBaseUrl)
                    ? (requestAuthorityBaseUrl ?? string.Empty).Trim().TrimEnd('/')
                    : configuredExternalBaseUrl.Trim().TrimEnd('/');
            }

            return ResolveAbsoluteBase(configuredExternalBaseUrl, requestUri, requestUri.AbsolutePath);
        }

        /// <summary>QR / scan URL base from the request URL and IIS application path.</summary>
        public static string ResolveBaseUrl(string configuredExternalBaseUrl, Uri requestUrl, string applicationPath)
        {
            return ResolveAbsoluteBase(configuredExternalBaseUrl, requestUrl, applicationPath);
        }

        public static string ResolvePasswordResetBaseUrl(string configuredExternalBaseUrl, Uri requestUrl)
        {
            return ResolvePasswordResetBaseUrl(configuredExternalBaseUrl, requestUrl, null);
        }

        /// <summary>Invite and password-reset link base (same rule as every other absolute URL).</summary>
        public static string ResolvePasswordResetBaseUrl(string configuredExternalBaseUrl, Uri requestUrl, string applicationPath)
        {
            return ResolveAbsoluteBase(configuredExternalBaseUrl, requestUrl, applicationPath);
        }

        /// <summary>
        /// The shared rule (no trailing slash). Returns "" when neither a configured base nor a request is available.
        /// </summary>
        public static string ResolveAbsoluteBase(string configuredExternalBaseUrl, Uri requestUrl, string applicationPath)
        {
            var appPath = NormalizeApplicationPath(applicationPath);
            var requestAuthority = BuildRequestAuthority(requestUrl);
            var requestBase = requestAuthority == null ? null : requestAuthority + appPath;

            if (string.IsNullOrWhiteSpace(configuredExternalBaseUrl))
            {
                return requestBase ?? string.Empty;
            }

            var configured = configuredExternalBaseUrl.Trim().TrimEnd('/');
            Uri configuredUri;
            if (!Uri.TryCreate(configured, UriKind.Absolute, out configuredUri))
            {
                return requestBase ?? configured;
            }

            if (requestUrl != null && ShouldPreferRequest(configuredUri, requestUrl))
            {
                return requestBase;
            }

            // "ExternalBaseUrl plus the app path": a host-only configured URL gets the IIS application path.
            var configuredPath = configuredUri.AbsolutePath.TrimEnd('/');
            if (configuredPath.Length == 0 && appPath.Length > 0)
            {
                return configured + appPath;
            }

            return configured;
        }

        /// <summary>
        /// Configured loopback host while the request is not (a live site left with http://localhost), or both
        /// loopback with a different port / scheme (local dev servers).
        /// </summary>
        public static bool ShouldPreferRequest(Uri configuredUri, Uri requestUrl)
        {
            if (requestUrl == null)
            {
                return false;
            }

            if (configuredUri == null)
            {
                return true;
            }

            var configuredLoopback = IsLoopbackHost(configuredUri.Host);
            var requestLoopback = IsLoopbackHost(requestUrl.Host);
            if (configuredLoopback && !requestLoopback)
            {
                return true;
            }

            return configuredLoopback && requestLoopback
                && (configuredUri.Port != requestUrl.Port
                    || !string.Equals(configuredUri.Scheme, requestUrl.Scheme, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>For settings validation: true when the URL's host is localhost / a loopback address.</summary>
        public static bool IsLoopbackUrl(string url)
        {
            Uri uri;
            return !string.IsNullOrWhiteSpace(url)
                && Uri.TryCreate(url.Trim(), UriKind.Absolute, out uri)
                && IsLoopbackHost(uri.Host);
        }

        public static string CombineBaseAndRelative(string baseUrl, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return relativePath ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return baseUrl;
            }

            var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
            return new Uri(baseUri, relativePath.TrimStart('/')).AbsoluteUri;
        }

        public static string NormalizeApplicationPath(string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(applicationPath))
            {
                return string.Empty;
            }

            var trimmed = applicationPath.Trim().TrimEnd('/');
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
        }

        private static string BuildRequestAuthority(Uri requestUrl)
        {
            if (requestUrl == null || !requestUrl.IsAbsoluteUri)
            {
                return null;
            }

            var builder = new UriBuilder(requestUrl.Scheme, requestUrl.Host);
            if (!IsDefaultPort(requestUrl.Scheme, requestUrl.Port))
            {
                builder.Port = requestUrl.Port;
            }

            return builder.Uri.GetLeftPart(UriPartial.Authority);
        }

        private static bool IsDefaultPort(string scheme, int port)
        {
            return (string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && port == 80)
                || (string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && port == 443);
        }

        public static bool IsLoopbackHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            IPAddress address;
            return IPAddress.TryParse(host.Trim('[', ']'), out address) && IPAddress.IsLoopback(address);
        }
    }
}
