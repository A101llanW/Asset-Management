using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Helpers
{
    public static class IncidentResolutionStatusHelper
    {
        public const string Open = "Open";
        public const string UnderReview = "Under review";
        public const string Closed = "Closed";
        public const string WrittenOff = "Written off";

        private static readonly string[] StandardStatuses =
        {
            Open,
            UnderReview,
            Closed,
            WrittenOff
        };

        public static string[] GetStandardStatuses()
        {
            return StandardStatuses;
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return StandardStatuses.Any(x => string.Equals(x, value.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            var match = StandardStatuses.FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                throw new InvalidOperationException("Unsupported resolution status.");
            }

            return match;
        }

        public static IList<KeyValuePair<string, string>> GetSelectOptions(string selectedValue)
        {
            var options = StandardStatuses
                .Select(x => new KeyValuePair<string, string>(x, x))
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedValue)
                && !options.Any(x => string.Equals(x.Key, selectedValue.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                options.Add(new KeyValuePair<string, string>(selectedValue.Trim(), selectedValue.Trim() + " (legacy)"));
            }

            return options;
        }
    }
}
