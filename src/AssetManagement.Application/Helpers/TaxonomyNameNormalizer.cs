using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Normalizes taxonomy labels to reduce duplicate categories, types, and sub-types
    /// that differ only by pluralization, casing, or known synonyms.
    /// </summary>
    public static class TaxonomyNameNormalizer
    {
        private static readonly Dictionary<string, string> Synonyms =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "IT", "Information Technology" },
                { "IT DEPT", "Information Technology" },
                { "ICT", "Information Technology" },
                { "Computers", "Desktop" },
                { "Computer", "Desktop" },
                { "Laptops", "Laptop" },
                { "Desktops", "Desktop" },
                { "Printers", "Printer" },
                { "Projectors", "Projector" },
                { "Routers", "Router" },
                { "Chairs", "Office Chair" },
                { "Desks", "Office Desk" },
                { "Tables", "Office Desk" },
                { "Monitors", "Monitor" },
                { "Servers", "Server" }
            };

        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();
        }

        public static string NormalizeDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var trimmed = Regex.Replace(value.Trim(), @"\s+", " ");
            string synonym;
            if (Synonyms.TryGetValue(trimmed, out synonym))
            {
                return synonym;
            }

            return ToTitleCase(trimmed);
        }

        public static bool NamesEquivalent(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            {
                return false;
            }

            if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var leftKey = NormalizeKey(NormalizeDisplayName(left));
            var rightKey = NormalizeKey(NormalizeDisplayName(right));
            if (leftKey == rightKey)
            {
                return true;
            }

            return IsPluralVariant(leftKey, rightKey);
        }

        public static string ResolveCanonicalName(string left, string right)
        {
            var leftCanonical = NormalizeDisplayName(left);
            var rightCanonical = NormalizeDisplayName(right);

            if (Synonyms.ContainsKey(left ?? string.Empty))
            {
                return leftCanonical;
            }

            if (Synonyms.ContainsKey(right ?? string.Empty))
            {
                return rightCanonical;
            }

            if (IsPluralOf(left, right))
            {
                return leftCanonical;
            }

            if (IsPluralOf(right, left))
            {
                return rightCanonical;
            }

            return leftCanonical.Length <= rightCanonical.Length ? leftCanonical : rightCanonical;
        }

        public static bool TryFindEquivalentName(string candidate, IEnumerable<string> existingNames, out string matchedName)
        {
            matchedName = null;
            if (string.IsNullOrWhiteSpace(candidate) || existingNames == null)
            {
                return false;
            }

            foreach (var existing in existingNames.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                if (NamesEquivalent(candidate, existing))
                {
                    matchedName = existing;
                    return true;
                }
            }

            return false;
        }

        private static bool IsPluralVariant(string leftKey, string rightKey)
        {
            return leftKey + "S" == rightKey || rightKey + "S" == leftKey;
        }

        private static bool IsPluralOf(string singularCandidate, string pluralCandidate)
        {
            if (string.IsNullOrWhiteSpace(singularCandidate) || string.IsNullOrWhiteSpace(pluralCandidate))
            {
                return false;
            }

            var singular = NormalizeKey(NormalizeDisplayName(singularCandidate));
            var plural = NormalizeKey(NormalizeDisplayName(pluralCandidate));
            return plural.Length > singular.Length && plural.StartsWith(singular, StringComparison.Ordinal)
                && (plural.EndsWith("S", StringComparison.Ordinal) || plural.EndsWith("ES", StringComparison.Ordinal));
        }

        private static string ToTitleCase(string value)
        {
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
        }
    }
}
