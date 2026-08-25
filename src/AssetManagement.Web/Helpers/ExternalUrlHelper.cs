using System;
using System.Configuration;
using System.Net;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Externally reachable base URLs using platform settings / Web.config ExternalBaseUrl when set.
    /// </summary>
    public static class ExternalUrlHelper
    {
        private const string DefaultBaseUrl = "http://localhost";

        public static Uri GetBaseUri(HttpRequestBase request)
        {
            return ResolveBaseUri(
                request != null ? request.Url : null,
                request != null ? request.ApplicationPath : null);
        }

        public static string GetTenantPortalUrl(HttpRequestBase request, string tenantSlug)
        {
            var baseUrl = GetBaseUri(request).ToString().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                return baseUrl;
            }

            return baseUrl + "/" + tenantSlug.Trim().TrimStart('/');
        }

        private static Uri ResolveBaseUri(Uri requestUrl, string applicationPath)
        {
            var configuredBaseUri = GetConfiguredBaseUri();
            var requestBaseUri = BuildRequestBaseUri(requestUrl, applicationPath);

            Uri resolved;
            if (configuredBaseUri != null)
            {
                resolved = ShouldPreferRequestUrl(configuredBaseUri, requestUrl, requestBaseUri)
                    ? (requestBaseUri ?? configuredBaseUri)
                    : configuredBaseUri;
            }
            else
            {
                resolved = requestBaseUri ?? new Uri(DefaultBaseUrl, UriKind.Absolute);
            }

            return resolved ?? new Uri(DefaultBaseUrl, UriKind.Absolute);
        }

        private static Uri GetConfiguredBaseUri()
        {
            var settingsService = DependencyResolver.Current != null
                ? DependencyResolver.Current.GetService<IPlatformSettingsService>()
                : null;
            var configured = settingsService != null
                ? settingsService.GetExternalBaseUrl()
                : ConfigurationManager.AppSettings["ExternalBaseUrl"];
            if (string.IsNullOrWhiteSpace(configured))
            {
                return null;
            }

            var trimmed = configured.Trim().TrimEnd('/');
            Uri parsedUri;
            return Uri.TryCreate(trimmed, UriKind.Absolute, out parsedUri) ? parsedUri : null;
        }

        private static Uri BuildRequestBaseUri(Uri requestUrl, string applicationPath)
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

            builder.Path = NormalizeApplicationPath(applicationPath);
            return builder.Uri;
        }

        private static string NormalizeApplicationPath(string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(applicationPath) || applicationPath == "/")
            {
                return "/";
            }

            return applicationPath.TrimEnd('/');
        }

        private static bool IsDefaultPort(string scheme, int port)
        {
            return (string.Equals(scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && port == 80)
                || (string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) && port == 443);
        }

        private static bool ShouldPreferRequestUrl(Uri configuredUri, Uri requestUrl, Uri requestBaseUri)
        {
            if (requestBaseUri == null || requestUrl == null)
            {
                return false;
            }

            if (configuredUri == null)
            {
                return true;
            }

            if (IsLoopbackHost(configuredUri.Host) && !IsLoopbackHost(requestUrl.Host))
            {
                return true;
            }

            return IsLocalDevPortOrSchemeMismatch(configuredUri, requestUrl);
        }

        private static bool IsLocalDevPortOrSchemeMismatch(Uri configuredUri, Uri requestUrl)
        {
            if (!IsLoopbackHost(configuredUri.Host) || !IsLoopbackHost(requestUrl.Host))
            {
                return false;
            }

            return configuredUri.Port != requestUrl.Port
                || !string.Equals(configuredUri.Scheme, requestUrl.Scheme, StringComparison.OrdinalIgnoreCase);
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
