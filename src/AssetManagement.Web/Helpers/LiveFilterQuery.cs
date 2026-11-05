using System;
using System.Collections.Specialized;
using System.Text;
using System.Web;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Builds list-page query strings for live filter / sort updates.
    /// Resets page to 1 on filter changes so results stay coherent.
    /// </summary>
    public static class LiveFilterQuery
    {
        public static NameValueCollection CopyQuery(NameValueCollection source)
        {
            var copy = HttpUtility.ParseQueryString(string.Empty);
            if (source == null)
            {
                return copy;
            }

            foreach (string key in source.AllKeys)
            {
                if (key == null)
                {
                    continue;
                }

                var values = source.GetValues(key);
                if (values == null || values.Length == 0)
                {
                    copy[key] = source[key];
                    continue;
                }

                foreach (var value in values)
                {
                    copy.Add(key, value);
                }
            }

            return copy;
        }

        public static NameValueCollection WithPageReset(NameValueCollection source, bool resetPage)
        {
            var copy = CopyQuery(source);
            if (resetPage)
            {
                copy["page"] = "1";
            }

            return copy;
        }

        public static string ToQueryString(NameValueCollection query)
        {
            if (query == null || query.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            var first = true;
            foreach (string key in query.AllKeys)
            {
                if (key == null)
                {
                    continue;
                }

                var values = query.GetValues(key);
                if (values == null)
                {
                    continue;
                }

                foreach (var value in values)
                {
                    if (!first)
                    {
                        sb.Append('&');
                    }

                    first = false;
                    sb.Append(Uri.EscapeDataString(key));
                    sb.Append('=');
                    sb.Append(Uri.EscapeDataString(value ?? string.Empty));
                }
            }

            return sb.ToString();
        }
    }
}
