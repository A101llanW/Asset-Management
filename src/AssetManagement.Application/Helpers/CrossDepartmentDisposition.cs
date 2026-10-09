using System;
using AssetManagement.Domain.Enums;

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

        /// <summary>
        /// rel1009h: a department-pool target (no person) that is not a room/class and differs from the asset's
        /// department (sub-department, department, grade) also needs Move or Keep - never move it silently.
        /// Room/Class pool targets always move the asset into that room.
        /// </summary>
        public static bool IsCrossDepartmentPool(
            int? assetDepartmentId,
            int? selectedDepartmentId,
            string toUserId,
            DepartmentKind? selectedKind)
        {
            if (!string.IsNullOrWhiteSpace(toUserId) || !selectedKind.HasValue)
            {
                return false;
            }

            if (!selectedDepartmentId.HasValue || !assetDepartmentId.HasValue || assetDepartmentId.Value <= 0
                || selectedDepartmentId.Value == assetDepartmentId.Value)
            {
                return false;
            }

            return selectedKind.Value != DepartmentKind.Room && selectedKind.Value != DepartmentKind.Class;
        }

        /// <summary>True when the form must ask Move or Keep (cross-department person or cross-department pool).</summary>
        public static bool NeedsChoice(
            int? assetDepartmentId,
            int? selectedDepartmentId,
            string toUserId,
            DepartmentKind? selectedKind)
        {
            return IsCrossDepartmentPerson(assetDepartmentId, selectedDepartmentId, toUserId)
                || IsCrossDepartmentPool(assetDepartmentId, selectedDepartmentId, toUserId, selectedKind);
        }

        /// <summary>
        /// Effective asset department including the rel1009h pool rule: a cross-department pool moves only on Move,
        /// otherwise the asset stays in its department (the assignment still records the selected department).
        /// </summary>
        public static int? ResolveEffectiveDepartmentId(
            int? assetDepartmentId,
            int? selectedDepartmentId,
            string toUserId,
            string disposition,
            DepartmentKind? selectedKind)
        {
            if (IsCrossDepartmentPool(assetDepartmentId, selectedDepartmentId, toUserId, selectedKind))
            {
                return Normalize(disposition) == Move ? selectedDepartmentId : assetDepartmentId;
            }

            return ResolveEffectiveDepartmentId(assetDepartmentId, selectedDepartmentId, toUserId, disposition);
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

            if (choice == Move)
            {
                return selectedDepartmentId;
            }

            // No explicit choice: keep the asset's current department (never change it silently).
            return assetDepartmentId.HasValue && assetDepartmentId.Value > 0
                ? assetDepartmentId
                : selectedDepartmentId;
        }
    }
}
