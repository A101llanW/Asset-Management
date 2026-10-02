using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    public static class SchoolDepartmentCodeHelper
    {
        private const int MaxAdminCodeLength = 8;
        private const int MaxSubCodeLength = 10;

        private static readonly Dictionary<string, string> KnownAdminCodes =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Administration", "ADMIN" },
                { "Information Technology", "IT" },
                { "Facilities", "FAC" },
                { "Procurement", "PROC" }
            };

        public static string NormalizeAdminDepartmentName(string departmentName)
        {
            var normalized = (departmentName ?? string.Empty).Trim();
            if (string.Equals(normalized, "IT", StringComparison.OrdinalIgnoreCase))
            {
                return "Information Technology";
            }

            if (string.Equals(normalized, "IT DEPT", StringComparison.OrdinalIgnoreCase))
            {
                return "Information Technology";
            }

            return normalized;
        }

        public static bool IsIctDepartmentName(string departmentName)
        {
            return string.Equals((departmentName ?? string.Empty).Trim(), "ICT", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsSharedScopeClassValue(string classOrSubUnitValue)
        {
            var normalized = (classOrSubUnitValue ?? string.Empty).Trim();
            return string.Equals(normalized, "ALL", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "All", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Maps ICT rows and IT rows with Class=ALL to Information Technology / ICT sub-department.
        /// </summary>
        public static bool TryResolveInformationTechnologySubUnit(
            string departmentName,
            string classOrSubUnitValue,
            out string parentDepartmentName,
            out string subUnitName)
        {
            parentDepartmentName = null;
            subUnitName = null;

            if (IsIctDepartmentName(departmentName))
            {
                parentDepartmentName = "Information Technology";
                subUnitName = string.IsNullOrWhiteSpace(classOrSubUnitValue) || IsSharedScopeClassValue(classOrSubUnitValue)
                    ? "ICT"
                    : classOrSubUnitValue.Trim();
                return true;
            }

            var normalizedAdmin = NormalizeAdminDepartmentName(departmentName);
            if (!string.Equals(normalizedAdmin, "Information Technology", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(classOrSubUnitValue))
            {
                return false;
            }

            parentDepartmentName = "Information Technology";
            subUnitName = IsSharedScopeClassValue(classOrSubUnitValue)
                ? "ICT"
                : classOrSubUnitValue.Trim();
            return true;
        }

        public static string BuildAdminDepartmentCode(string departmentName)
        {
            var normalizedName = NormalizeAdminDepartmentName(departmentName);
            string knownCode;
            if (KnownAdminCodes.TryGetValue(normalizedName, out knownCode))
            {
                return knownCode;
            }

            return DeriveCodeFromName(normalizedName, MaxAdminCodeLength);
        }

        public static string BuildSubDepartmentCode(string parentCode, string subDepartmentName)
        {
            var parentToken = NormalizeToken(parentCode, MaxAdminCodeLength);
            var subToken = NormalizeToken(subDepartmentName, MaxSubCodeLength);
            if (string.IsNullOrWhiteSpace(subToken))
            {
                throw new ArgumentException("Sub-department name is required.", "subDepartmentName");
            }

            return parentToken + "-" + subToken;
        }

        public static string BuildRoomCode(string parentCode, string roomName)
        {
            var parentToken = NormalizeToken(parentCode, MaxSubCodeLength);
            var roomToken = NormalizeToken(roomName, MaxSubCodeLength);
            if (string.IsNullOrWhiteSpace(roomToken))
            {
                throw new ArgumentException("Room name is required.", "roomName");
            }

            return parentToken + "-" + roomToken;
        }

        public static bool ShouldResolveAsSubDepartment(string departmentName, string classOrSubUnitValue)
        {
            return !SchoolClassCodeHelper.IsClassroomDepartment(departmentName)
                && !string.IsNullOrWhiteSpace(departmentName)
                && !string.IsNullOrWhiteSpace(classOrSubUnitValue);
        }

        public static bool IsAdministrativeDepartmentName(string departmentName)
        {
            return !string.IsNullOrWhiteSpace(departmentName)
                && !SchoolClassCodeHelper.IsClassroomDepartment(departmentName);
        }

        private static string DeriveCodeFromName(string name, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "GEN";
            }

            var words = name.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2)
            {
                var compact = string.Concat(Array.ConvertAll(words, word =>
                    word.Length >= 3 ? word.Substring(0, 3) : word));
                return NormalizeToken(compact, maxLength);
            }

            return NormalizeToken(words[0], maxLength);
        }

        private static string NormalizeToken(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GEN";
            }

            var cleaned = Regex.Replace(value.Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
            if (cleaned.Length == 0)
            {
                return "GEN";
            }

            return cleaned.Length <= maxLength ? cleaned : cleaned.Substring(0, maxLength);
        }
    }
}
