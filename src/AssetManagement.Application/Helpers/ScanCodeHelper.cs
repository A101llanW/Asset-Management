using System;
using System.Text;

namespace AssetManagement.Application.Helpers
{
    public static class ScanCodeHelper
    {
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var trimmed = raw.Trim();
            if (!LooksLikeUrl(trimmed))
            {
                return trimmed;
            }

            var queryStart = trimmed.IndexOf('?');
            if (queryStart >= 0)
            {
                var code = ExtractQueryParameter(trimmed.Substring(queryStart + 1), "code");
                if (!string.IsNullOrWhiteSpace(code))
                {
                    return Uri.UnescapeDataString(code.Trim());
                }
            }

            return trimmed;
        }

        /// <summary>
        /// Builds a case-insensitive lookup key that ignores spaces and hyphens.
        /// </summary>
        public static string ToLookupKey(string raw)
        {
            var normalized = Normalize(raw);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            var builder = new StringBuilder(normalized.Length);
            foreach (var character in normalized)
            {
                if (char.IsWhiteSpace(character) || character == '-')
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(character));
            }

            return builder.Length == 0 ? null : builder.ToString();
        }

        public static bool FieldMatchesLookupKey(string fieldValue, string lookupKey)
        {
            if (string.IsNullOrWhiteSpace(fieldValue) || string.IsNullOrWhiteSpace(lookupKey))
            {
                return false;
            }

            return string.Equals(ToLookupKey(fieldValue), lookupKey, StringComparison.Ordinal);
        }

        private static bool LooksLikeUrl(string value)
        {
            return value.IndexOf("://", StringComparison.Ordinal) >= 0
                || value.IndexOf('?') >= 0
                || value.StartsWith("/", StringComparison.Ordinal);
        }

        private static string ExtractQueryParameter(string query, string name)
        {
            var pairs = query.Split('&');
            foreach (var pair in pairs)
            {
                var separator = pair.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                var key = pair.Substring(0, separator);
                if (!key.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return pair.Substring(separator + 1);
            }

            return null;
        }
    }
}
