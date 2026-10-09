using System;
using System.Web;
using System.Web.Mvc;

namespace AssetManagement.Web.Helpers
{
    public static class AssetQrUrlHelper
    {
        public static string BuildScanUrl(HttpRequestBase request, UrlHelper urlHelper, string assetTag, string organizationSlug = null, string externalBaseUrl = null)
        {
            if (request == null || urlHelper == null || string.IsNullOrWhiteSpace(assetTag))
            {
                return string.Empty;
            }

            var tenant = organizationSlug ?? TenantUrlHelper.GetTenantToken(request.RequestContext.HttpContext);
            string relative;
            if (TenantUrlHelper.IsValidTenantSlug(tenant))
            {
                relative = TenantUrlHelper.BuildTenantPath(tenant, "AssetScan", "Lookup")
                    + "?code=" + HttpUtility.UrlEncode(assetTag.Trim());
            }
            else
            {
                relative = urlHelper.Action("Lookup", "AssetScan", new { code = assetTag.Trim() });
            }

            // rel1009g: configured ExternalBaseUrl (+ app path) unless it is localhost and the request is not.
            var configured = string.IsNullOrWhiteSpace(externalBaseUrl) ? ExternalUrlHelper.GetConfiguredBaseUrl() : externalBaseUrl;
            var baseUrl = AssetManagement.Application.Helpers.AssetScanUrlHelper.ResolveBaseUrl(configured, request.Url, request.ApplicationPath);
            if (!string.IsNullOrWhiteSpace(baseUrl))
            {
                return AppPathHelper.CombineBaseAndAppRelative(baseUrl, relative);
            }

            return new Uri(request.Url, relative).AbsoluteUri;
        }

        /// <summary>Sample asset tag used for the designer's sample scan URL and the worst-case QR payload.</summary>
        public const string SampleAssetTag = "AST-1001";

        /// <summary>
        /// rel1009i: worst-case scan-URL length (bytes) for this deployment and organisation, used to cap the QR
        /// magnification (designer, save, load, print config). See LabelQrSizing.WorstCasePayloadBytes.
        /// </summary>
        public static int WorstCaseQrPayloadBytes(HttpRequestBase request, UrlHelper urlHelper, string externalBaseUrl = null)
        {
            string sample = null;
            try
            {
                sample = BuildScanUrl(request, urlHelper, SampleAssetTag, externalBaseUrl: externalBaseUrl);
            }
            catch (Exception)
            {
                // Fall back to the default worst case.
            }

            return AssetManagement.Application.Helpers.LabelQrSizing.WorstCasePayloadBytes(sample, SampleAssetTag);
        }
    }
}
