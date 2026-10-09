using System;
using System.Configuration;
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

        /// <summary>The configured ExternalBaseUrl (platform setting, else Web.config appSetting); null when unset.</summary>
        public static string GetConfiguredBaseUrl()
        {
            try
            {
                var settingsService = DependencyResolver.Current != null
                    ? DependencyResolver.Current.GetService<IPlatformSettingsService>()
                    : null;
                var configured = settingsService != null
                    ? settingsService.GetExternalBaseUrl()
                    : ConfigurationManager.AppSettings["ExternalBaseUrl"];
                return string.IsNullOrWhiteSpace(configured) ? null : configured.Trim();
            }
            catch (InvalidOperationException)
            {
                return ConfigurationManager.AppSettings["ExternalBaseUrl"];
            }
        }

        /// <summary>
        /// rel1009g: ExternalBaseUrl (+ app path when host-only), or request + app path when the configured base is
        /// loopback and the request is not (AssetScanUrlHelper.ResolveAbsoluteBase, shared with QR / e-mail links).
        /// </summary>
        public static Uri ResolveBaseUri(Uri requestUrl, string applicationPath, string configuredBaseUrl)
        {
            var resolved = AssetManagement.Application.Helpers.AssetScanUrlHelper.ResolveAbsoluteBase(configuredBaseUrl, requestUrl, applicationPath);
            Uri uri;
            if (!string.IsNullOrWhiteSpace(resolved) && Uri.TryCreate(resolved.TrimEnd('/') + "/", UriKind.Absolute, out uri))
            {
                return uri;
            }

            System.Diagnostics.Trace.TraceWarning(
                "ExternalUrlHelper: no ExternalBaseUrl and no request URL; falling back to {0}. Set ExternalBaseUrl in Platform settings.",
                DefaultBaseUrl);
            return new Uri(DefaultBaseUrl + "/", UriKind.Absolute);
        }

        private static Uri ResolveBaseUri(Uri requestUrl, string applicationPath)
        {
            return ResolveBaseUri(requestUrl, applicationPath, GetConfiguredBaseUrl());
        }
    }
}
