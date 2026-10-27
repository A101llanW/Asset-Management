using AssetManagement.Application.DTOs;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// The one place that decides how long an asset / item name may be. Used by every view-model attribute, the
    /// AssetService guard, the requisition line "Item / asset name", the PO line and receiving (name per line), and
    /// the JS maxlength (rendered from this constant). The DB columns (Asset.AssetName and the migration 108 ItemName
    /// columns) are NVARCHAR(200), the same as this limit.
    /// </summary>
    public static class AssetNameRules
    {
        // 200, pending Allan's confirmation (relayed by PM 20:58 EAT); the same as the Asset.AssetName and ItemName NVARCHAR(200) columns.
        public const int MaxLength = 200;

        public const string DisplayName = "Asset name";

        public const string RequiredMessage = "Enter the asset name.";

        public static readonly string TooLongMessage = "Asset name cannot be longer than " + MaxLength + " characters.";

        /// <summary>Trimmed value, or null when blank.</summary>
        public static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>Trims and checks a required name; throws BusinessException with clear text (label = e.g. "line 2").</summary>
        public static string EnsureValid(string value, string label = null)
        {
            var suffix = string.IsNullOrWhiteSpace(label) ? string.Empty : " (" + label + ")";
            var name = Normalize(value);
            if (name == null)
            {
                throw new BusinessException(RequiredMessage.TrimEnd('.') + suffix + ".");
            }

            if (name.Length > MaxLength)
            {
                throw new BusinessException(TooLongMessage.TrimEnd('.') + suffix + ": " + name.Length + " entered.");
            }

            return name;
        }

        /// <summary>For system-derived names (legacy fallback chain): trimmed and cut to the limit, never an error.</summary>
        public static string Fit(string value)
        {
            var name = Normalize(value);
            if (name == null)
            {
                return null;
            }

            return name.Length > MaxLength ? name.Substring(0, MaxLength).TrimEnd() : name;
        }
    }
}
