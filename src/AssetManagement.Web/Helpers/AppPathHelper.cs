using System;
using System.Web;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// IIS application (virtual directory) base-path helpers so the app works both at site root
    /// (e.g. http://localhost:8080/) and as a sub-application (e.g. https://host/NanoAssetSuit/).
    /// Pattern borrowed from HireHub (HR.Web\Helpers\TenantAuthRedirectHelper.GetApplicationVirtualPath /
    /// StripApplicationVirtualPath and HR.Web\Helpers\ExternalUrlHelper.StripApplicationPathPrefix).
    /// Outside ASP.NET (unit tests, worker) HttpRuntime.AppDomainAppVirtualPath is null, so the base path is
    /// empty and every method returns the input unchanged.
    /// </summary>
    public static class AppPathHelper
    {
        /// <summary>IIS application virtual path ("/" at site root, "/NanoAssetSuit" under a sub-app).</summary>
        public static string GetApplicationVirtualPath()
        {
            try
            {
                return HttpRuntime.AppDomainAppVirtualPath ?? "/";
            }
            catch
            {
                return "/";
            }
        }

        /// <summary>Application base path without trailing slash: "" at site root, "/NanoAssetSuit" under a sub-app.</summary>
        public static string GetAppBasePath()
        {
            return NormalizeBasePath(GetApplicationVirtualPath());
        }

        public static string NormalizeBasePath(string applicationPath)
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

        /// <summary>
        /// Turns an app-relative path ("~/Account/Login" or "/Account/Login") into an app-absolute path
        /// ("/NanoAssetSuit/Account/Login" under a sub-app, unchanged at root). Absolute URLs and
        /// protocol-relative URLs are returned unchanged.
        /// </summary>
        public static string ToAppAbsolute(string appRelativePath)
        {
            return ToAppAbsolute(appRelativePath, GetApplicationVirtualPath());
        }

        public static string ToAppAbsolute(string appRelativePath, string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(appRelativePath))
            {
                return appRelativePath;
            }

            var path = appRelativePath.Trim();
            if (path.StartsWith("//", StringComparison.Ordinal) || IsAbsoluteHttpUrl(path))
            {
                return path;
            }

            if (path.StartsWith("~", StringComparison.Ordinal))
            {
                path = path.Substring(1);
            }

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
                path = "/" + path;
            }

            return NormalizeBasePath(applicationPath) + path;
        }

        /// <summary>
        /// Removes the IIS application path prefix from a request path so route-segment parsing
        /// (tenant/controller/action) behaves the same at root and under a sub-app.
        /// </summary>
        public static string StripApplicationVirtualPath(string path)
        {
            return StripApplicationVirtualPath(path, GetApplicationVirtualPath());
        }

        public static string StripApplicationVirtualPath(string path, string applicationPath)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            var basePath = NormalizeBasePath(applicationPath);
            if (basePath.Length == 0)
            {
                return path;
            }

            if (string.Equals(path, basePath, StringComparison.OrdinalIgnoreCase))
            {
                return "/";
            }

            if (path.StartsWith(basePath + "/", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(basePath + "?", StringComparison.OrdinalIgnoreCase))
            {
                var remainder = path.Substring(basePath.Length);
                return remainder.StartsWith("/", StringComparison.Ordinal) ? remainder : "/" + remainder;
            }

            return path;
        }

        /// <summary>
        /// Combines an absolute base URL (ExternalBaseUrl or request authority) with an app-absolute path.
        /// When the base URL already ends with the IIS application path (e.g. https://host/NanoAssetSuit)
        /// and the relative path also starts with it, the duplicate segment is removed (HireHub ExternalUrlHelper.ToAbsoluteUrl rule).
        /// </summary>
        public static string CombineBaseAndAppRelative(string baseUrl, string appAbsolutePath)
        {
            return CombineBaseAndAppRelative(baseUrl, appAbsolutePath, GetApplicationVirtualPath());
        }

        public static string CombineBaseAndAppRelative(string baseUrl, string appAbsolutePath, string applicationPath)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                return appAbsolutePath ?? string.Empty;
            }

            var trimmedBase = baseUrl.Trim().TrimEnd('/');
            if (string.IsNullOrWhiteSpace(appAbsolutePath))
            {
                return trimmedBase;
            }

            var path = appAbsolutePath.StartsWith("/", StringComparison.Ordinal) ? appAbsolutePath : "/" + appAbsolutePath;
            var basePath = NormalizeBasePath(applicationPath);
            Uri baseUri;
            if (basePath.Length > 0
                && Uri.TryCreate(trimmedBase, UriKind.Absolute, out baseUri)
                && baseUri.AbsolutePath.TrimEnd('/').EndsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                path = StripApplicationVirtualPath(path, basePath);
            }

            return trimmedBase + path;
        }

        private static bool IsAbsoluteHttpUrl(string value)
        {
            return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }
    }
}
