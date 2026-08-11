using System.Linq;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Application
{
    public static class DisplayText
    {
        public const string Empty = "";

        public const string Unassigned = "Unassigned";

        public static string FormatBrandModel(string brand, string model)
        {
            var normalizedBrand = LegacyImportDefaults.NormalizeForDisplay(brand, LegacyImportDefaults.Brand);
            var normalizedModel = LegacyImportDefaults.NormalizeForDisplay(model, LegacyImportDefaults.Model);
            return string.Join(" ", new[] { normalizedBrand, normalizedModel }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }
    }
}
