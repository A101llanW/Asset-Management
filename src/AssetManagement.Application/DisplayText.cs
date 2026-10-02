using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application
{
    public static class DisplayText
    {
        public const string Empty = "";

        public const string Unassigned = "Unassigned";

        public const string DepartmentPool = "Department pool";

        public static string FormatBrandModel(string brand, string model)
        {
            var normalizedBrand = LegacyImportDefaults.NormalizeForDisplay(brand, LegacyImportDefaults.Brand);
            var normalizedModel = LegacyImportDefaults.NormalizeForDisplay(model, LegacyImportDefaults.Model);
            return string.Join(" ", new[] { normalizedBrand, normalizedModel }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        /// <summary>
        /// Person name when set; for Assigned assets with no person, show department-pool custody
        /// (CurrentCustodianId intentionally null). Otherwise Unassigned.
        /// </summary>
        public static string FormatCustodian(string custodianName, AssetStatus status)
        {
            if (!string.IsNullOrWhiteSpace(custodianName))
            {
                return custodianName;
            }

            if (status == AssetStatus.Assigned)
            {
                return DepartmentPool;
            }

            return Unassigned;
        }
    }
}
