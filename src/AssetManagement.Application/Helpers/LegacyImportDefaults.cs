using System;

namespace AssetManagement.Application.Helpers
{
    public static class LegacyImportDefaults
    {
        public const string Brand = "Unknown";
        public const string Model = "Legacy Import";
        public const decimal AcquisitionCost = 0.01m;

        public static string NormalizeForDisplay(string value, string placeholder)
        {
            var normalized = NormalizeForStorage(value, placeholder);
            return string.IsNullOrWhiteSpace(normalized) ? string.Empty : normalized;
        }

        public static string NormalizeForStorage(string value, string placeholder)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized)
                || string.Equals(normalized, placeholder, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return normalized;
        }
    }
}
