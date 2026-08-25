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

            return normalized;
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

        public static string BuildSubDepartmentCode(string parentCode, string subUnitName)
        {
            var parentToken = NormalizeToken(parentCode, MaxAdminCodeLength);
            var subToken = NormalizeToken(subUnitName, MaxSubCodeLength);
            if (string.IsNullOrWhiteSpace(subToken))
            {
                throw new ArgumentException("Sub-unit name is required.", "subUnitName");
            }

            return parentToken + "-" + subToken;
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
                return NormalizeToken(string.Concat(Array.ConvertAll(words, word => word[0].ToString())), maxLength);
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
