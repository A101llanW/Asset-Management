using System;
using System.Web;
using System.Web.Mvc;

namespace AssetManagement.Web.Helpers
{
    public static class AssetQrUrlHelper
    {
        public static string BuildScanUrl(HttpRequestBase request, UrlHelper urlHelper, string assetTag)
        {
            if (request == null || urlHelper == null || string.IsNullOrWhiteSpace(assetTag))
            {
                return string.Empty;
            }

            var relative = urlHelper.Action("Lookup", "AssetScan", new { code = assetTag.Trim() });
            return new Uri(request.Url, relative).AbsoluteUri;
        }
    }
}
