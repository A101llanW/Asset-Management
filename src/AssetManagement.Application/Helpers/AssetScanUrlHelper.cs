using System;
using System.Net;

namespace AssetManagement.Application.Helpers
{
    public static class AssetScanUrlHelper
    {
        public static string ResolveBaseUrl(string configuredExternalBaseUrl, string requestAuthorityBaseUrl)
        {
            if (!string.IsNullOrWhiteSpace(configuredExternalBaseUrl))
            {
                return configuredExternalBaseUrl.Trim().TrimEnd('/');
            }

            if (!string.IsNullOrWhiteSpace(requestAuthorityBaseUrl))
            {
                return requestAuthorityBaseUrl.Trim().TrimEnd('/');
            }

            return string.Empty;
        }

        public static string ResolvePasswordResetBaseUrl(string configuredExternalBaseUrl, Uri requestUrl)
        {
            var requestAuthority = BuildRequestAuthority(requestUrl);
            if (string.IsNullOrWhiteSpace(configuredExternalBaseUrl))
            {
                return requestAuthority ?? string.Empty;
            }

            var configured = configuredExternalBaseUrl.Trim().TrimEnd('/');
            Uri configuredUri;
            if (!Uri.TryCreate(configured, UriKind.Absolute, out configuredUri))
            {
                return configured;
            }

            if (requestUrl == null)
            {
                return configured;
            }

            Uri requestAuthorityUri;
            if (!Uri.TryCreate(requestAuthority, UriKind.Absolute, out requestAuthorityUri))
            {
                return configured;
            }

            if (IsLoopbackHost(configuredUri.Host)
                && IsLoopbackHost(requestAuthorityUri.Host)
                && configuredUri.Port != requestAuthorityUri.Port)
            {
                return requestAuthority;
            }

            return configured;
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

        private static string BuildRequestAuthority(Uri requestUrl)
        {
            if (requestUrl == null)
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

        private static bool IsLoopbackHost(string host)
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
            return IPAddress.TryParse(host, out address) && IPAddress.IsLoopback(address);
        }
    }
}
