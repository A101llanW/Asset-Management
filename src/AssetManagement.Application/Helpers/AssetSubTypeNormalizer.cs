using System;
using System.Text.RegularExpressions;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class AssetSubTypeNormalizer
    {
        public const string NameSeparator = " - ";

        private static readonly Regex SeparatorSpacingRegex = new Regex(
            @"\s*[-\u2013\u2014\u00A0\uFFFD\u2022\u2212\u00B7\u00D7\u002A]\s*",
            RegexOptions.Compiled);

        public static string NormalizeBrand(string brand)
        {
            return string.IsNullOrWhiteSpace(brand) ? string.Empty : brand.Trim();
        }

        public static string NormalizeModel(string model)
        {
            return string.IsNullOrWhiteSpace(model) ? string.Empty : model.Trim();
        }

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            var value = name.Trim();
            // UTF-8 en/em dash bytes misread as Windows-1252 (e.g. "Apple â€" MacBook").
            value = value.Replace("\u00E2\u20AC\u2019", "-");
            value = value.Replace("\u00E2\u20AC\u201C", "-");
            value = value.Replace("\u00E2\u20AC\u201D", "-");
            value = value.Replace("\u00C3\u00A2\u00E2\u0082\u00AC\u00E2\u0080\u0093", "-");
            value = value.Replace("\u2013", "-");
            value = value.Replace("\u2014", "-");
            value = value.Replace("\u2212", "-");
            value = value.Replace("\uFFFD", "-");
            value = value.Replace("\u00A0", " ");
            return CollapseSeparatorSpacing(value);
        }

        public static bool BrandModelEquals(string leftBrand, string leftModel, string rightBrand, string rightModel)
        {
            return string.Equals(
                NormalizeBrand(leftBrand),
                NormalizeBrand(rightBrand),
                StringComparison.OrdinalIgnoreCase)
                && string.Equals(
                    NormalizeModel(leftModel),
                    NormalizeModel(rightModel),
                    StringComparison.OrdinalIgnoreCase);
        }

        public static string BuildSuggestedName(string brand, string model, AssetType assetType = null)
        {
            var normalizedBrand = NormalizeBrand(brand);
            var normalizedModel = NormalizeModel(model);
            if (!string.IsNullOrEmpty(normalizedBrand) && !string.IsNullOrEmpty(normalizedModel))
            {
                return normalizedBrand + NameSeparator + normalizedModel;
            }

            if (!string.IsNullOrEmpty(normalizedBrand))
            {
                return normalizedBrand;
            }

            if (!string.IsNullOrEmpty(normalizedModel))
            {
                return normalizedModel;
            }

            if (assetType != null && !string.IsNullOrWhiteSpace(assetType.Name))
            {
                return assetType.Name.Trim();
            }

            return "Unspecified item";
        }

        private static string CollapseSeparatorSpacing(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return SeparatorSpacingRegex.Replace(value, NameSeparator);
        }
    }
}
