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
                { "Procurement", "PROC" },
                { "Academics", "ACADEMICS" },
                { "Support", "SUPPORT" },
                { "Boarding", "BOARDING" }
            };

        /// <summary>
        /// Top-level administrative pillars (L35160674 / school template). Free-text room names are not pillars.
        /// </summary>
        private static readonly HashSet<string> KnownAdministrativePillars =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Administration",
                "Academics",
                "Support",
                "Boarding",
                "Information Technology",
                "Facilities",
                "Procurement"
            };

        private static readonly Regex RoomLikeNameRegex = new Regex(
            @"(?i)(\broom\b|\blab\b|studio|\boffice\b|reception\s+area|\bmeet\b|\bav\b|library\s+room)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

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
                && !string.IsNullOrWhiteSpace(classOrSubUnitValue)
                && !IsRoomLikeDepartmentName(classOrSubUnitValue);
        }

        public static bool IsKnownAdministrativePillarName(string departmentName)
        {
            if (string.IsNullOrWhiteSpace(departmentName)
                || SchoolClassCodeHelper.IsClassroomDepartment(departmentName))
            {
                return false;
            }

            var normalized = NormalizeAdminDepartmentName(departmentName);
            return KnownAdministrativePillars.Contains(normalized);
        }

        /// <summary>
        /// Room-like physical spaces (Art room, Biology Lab, Meet Room, etc.). Not Classroom and not admin pillars.
        /// </summary>
        public static bool IsRoomLikeDepartmentName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)
                || SchoolClassCodeHelper.IsClassroomDepartment(name)
                || IsKnownAdministrativePillarName(name)
                || IsIctDepartmentName(name))
            {
                return false;
            }

            return RoomLikeNameRegex.IsMatch(name.Trim());
        }

        /// <summary>
        /// True for non-classroom names that should provision as Administrative (pillars or other non-room free text).
        /// Room-like names return false so import can create DepartmentKind.Room instead.
        /// </summary>
        public static bool IsAdministrativeDepartmentName(string departmentName)
        {
            return !string.IsNullOrWhiteSpace(departmentName)
                && !SchoolClassCodeHelper.IsClassroomDepartment(departmentName)
                && !IsRoomLikeDepartmentName(departmentName);
        }

        /// <summary>
        /// Best-effort default parent for a standalone room name (Department column only).
        /// Returns Admin or Sub display name used by import provisioning.
        /// </summary>
        public static bool TryResolveDefaultRoomParent(
            string roomName,
            out string parentDepartmentName,
            out bool parentIsSubDepartment,
            out string parentAdminPillarName)
        {
            parentDepartmentName = null;
            parentIsSubDepartment = false;
            parentAdminPillarName = null;

            if (string.IsNullOrWhiteSpace(roomName))
            {
                return false;
            }

            var n = roomName.Trim().ToLowerInvariant();

            if (n.Contains("art") || n.Contains("music"))
            {
                parentDepartmentName = "Entertainment";
                parentIsSubDepartment = true;
                parentAdminPillarName = "Academics";
                return true;
            }

            if (n.Contains("biol") || n.Contains("chem") || n.Contains("phys") || n.Contains("science")
                || (n.Contains("lab") && (n.Contains("bio") || n.Contains("che") || n.Contains("phy"))))
            {
                parentDepartmentName = "Science";
                parentIsSubDepartment = true;
                parentAdminPillarName = "Academics";
                return true;
            }

            if (n.Contains("food") && n.Contains("lab"))
            {
                parentDepartmentName = "Science";
                parentIsSubDepartment = true;
                parentAdminPillarName = "Academics";
                return true;
            }

            if (n.Contains("sport") || n.Contains("field") || n.Contains("green"))
            {
                parentDepartmentName = "Sports";
                parentIsSubDepartment = true;
                parentAdminPillarName = "Support";
                return true;
            }

            if (n.Contains("counsel") || n.Contains("counsellor") || n.Contains("counselor"))
            {
                parentDepartmentName = "Counselling";
                parentIsSubDepartment = true;
                parentAdminPillarName = "Support";
                return true;
            }

            if (n.Contains("av") || n.Contains("media") || n.Contains("studio")
                || n.Contains("it office") || n.Contains("james it"))
            {
                parentDepartmentName = "Support";
                parentIsSubDepartment = false;
                parentAdminPillarName = "Support";
                return true;
            }

            if (n.Contains("meet") || n.Contains("reception") || n.Contains("teacher"))
            {
                parentDepartmentName = "Administration";
                parentIsSubDepartment = false;
                parentAdminPillarName = "Administration";
                return true;
            }

            if (n.Contains("library"))
            {
                parentDepartmentName = "Administration";
                parentIsSubDepartment = false;
                parentAdminPillarName = "Administration";
                return true;
            }

            // Generic rooms/labs/offices: under Academics
            parentDepartmentName = "Academics";
            parentIsSubDepartment = false;
            parentAdminPillarName = "Academics";
            return true;
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
