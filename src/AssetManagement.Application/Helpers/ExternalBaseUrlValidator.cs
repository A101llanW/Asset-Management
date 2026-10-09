using System;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// rel1009g: non-blocking checks for the Platform "ExternalBaseUrl" setting (used for QR, invite, reset and
    /// portal links). Returns null when fine, otherwise a warning to show after saving.
    /// </summary>
    public static class ExternalBaseUrlValidator
    {
        public static string Validate(string externalBaseUrl, string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(externalBaseUrl))
            {
                return "ExternalBaseUrl is empty; links fall back to the address of the request that creates them.";
            }

            Uri uri;
            if (!Uri.TryCreate(externalBaseUrl.Trim(), UriKind.Absolute, out uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return "ExternalBaseUrl is not an absolute http(s) URL (e.g. https://nanosoft.africa/NanoAssetSuite).";
            }

            if (AssetScanUrlHelper.IsLoopbackHost(uri.Host))
            {
                return "ExternalBaseUrl points at localhost (" + uri.Host + "). Links opened from other machines will not work; requests from a public host name use that host instead.";
            }

            var appPath = AssetScanUrlHelper.NormalizeApplicationPath(applicationPath);
            var configuredPath = uri.AbsolutePath.TrimEnd('/');
            if (appPath.Length > 0
                && configuredPath.Length > 0
                && !configuredPath.EndsWith(appPath, StringComparison.OrdinalIgnoreCase))
            {
                return "ExternalBaseUrl path '" + configuredPath + "' does not end with this site's application path '" + appPath + "'.";
            }

            if (appPath.Length > 0 && configuredPath.Length == 0)
            {
                return "ExternalBaseUrl has no application path; '" + appPath + "' is appended automatically. Consider saving " + uri.GetLeftPart(UriPartial.Authority) + appPath + ".";
            }

            return null;
        }
    }
}
