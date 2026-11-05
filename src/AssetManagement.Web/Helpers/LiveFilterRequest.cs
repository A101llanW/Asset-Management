using System;
using System.Web;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Detects live list-filter AJAX requests (as-you-type search/sort/filter).
    /// The client sends header X-Live-Filter: 1; views then use a chrome-free layout
    /// so the browser can swap only the results container.
    /// </summary>
    public static class LiveFilterRequest
    {
        public const string HeaderName = "X-Live-Filter";
        public const string HeaderValue = "1";

        public static bool IsLiveFilter(HttpRequestBase request)
        {
            if (request == null || request.Headers == null)
            {
                return false;
            }

            var value = request.Headers[HeaderName];
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return string.Equals(value.Trim(), HeaderValue, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.Trim(), "true", StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveLayout(HttpRequestBase request, string defaultLayout, string liveFilterLayout)
        {
            if (IsLiveFilter(request) && !string.IsNullOrWhiteSpace(liveFilterLayout))
            {
                return liveFilterLayout;
            }

            return defaultLayout;
        }
    }
}
