using System;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// When assigning/transferring to a person whose selected department differs from the asset's
    /// current department, the operator must choose Move (update asset department) or Keep
    /// (ownership only; asset stays in its current department).
    /// </summary>
    public static class CrossDepartmentDisposition
    {
        public const string Move = "Move";
        public const string Keep = "Keep";

        public static bool IsCrossDepartmentPerson(
            int? assetDepartmentId,
            int? selectedDepartmentId,
            string toUserId)
        {
            if (string.IsNullOrWhiteSpace(toUserId))
            {
                return false;
            }

            if (!selectedDepartmentId.HasValue || !assetDepartmentId.HasValue || assetDepartmentId.Value <= 0)
            {
                return false;
            }

            return selectedDepartmentId.Value != assetDepartmentId.Value;
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (string.Equals(trimmed, Move, StringComparison.OrdinalIgnoreCase))
            {
                return Move;
            }

            if (string.Equals(trimmed, Keep, StringComparison.OrdinalIgnoreCase))
            {
                return Keep;
            }

            return null;
        }

        public static bool IsValidChoice(string value)
        {
            return Normalize(value) != null;
        }

        /// <summary>
        /// Resolves the department id that should be written on the asset / custody row
        /// after the operator's Move vs Keep choice.
        /// </summary>
        public static int? ResolveEffectiveDepartmentId(
            int? assetDepartmentId,
            int? selectedDepartmentId,
            string toUserId,
            string disposition)
        {
            if (!IsCrossDepartmentPerson(assetDepartmentId, selectedDepartmentId, toUserId))
            {
                return selectedDepartmentId;
            }

            var choice = Normalize(disposition);
            if (choice == Keep)
            {
                return assetDepartmentId.HasValue && assetDepartmentId.Value > 0
                    ? assetDepartmentId
                    : selectedDepartmentId;
            }

            // Move (or missing choice - caller should prompt before reaching here)
            return selectedDepartmentId;
        }
    }
}
