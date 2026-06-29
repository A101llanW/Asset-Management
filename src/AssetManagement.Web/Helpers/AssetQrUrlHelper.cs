using System;
using System.Web;
using System.Web.Mvc;

namespace AssetManagement.Web.Helpers
{
    public static class AssetQrUrlHelper
    {
        public static string BuildScanUrl(HttpRequestBase request, UrlHelper urlHelper, string assetTag, string organizationSlug = null)
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

            return new Uri(request.Url, relative).AbsoluteUri;
        }
    }
}
