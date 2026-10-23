using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Section (Prep / Senior school) labels, parsing, ordering and the grade sort number (migration 106).
    /// A null section always means Senior school: that is what every pre-existing row is.
    /// </summary>
    public static class DepartmentSectionHelper
    {
        public const string PrepLabel = "Prep";
        public const string SeniorLabel = "Senior school";

        private static readonly Regex GradeCodeRegex = new Regex(
            @"^G(\d{1,2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static DepartmentSection Normalize(DepartmentSection? section)
        {
            return section ?? DepartmentSection.SeniorSchool;
        }

        public static string Label(DepartmentSection? section)
        {
            return Normalize(section) == DepartmentSection.Prep ? PrepLabel : SeniorLabel;
        }

        /// <summary>Prep first (0), then Senior school (1).</summary>
        public static int Rank(DepartmentSection? section)
        {
            return Normalize(section) == DepartmentSection.Prep ? 0 : 1;
        }

        /// <summary>Accepts Prep, Senior, SeniorSchool, "Senior school", 0 and 1 (case-insensitive). Blank or unknown = null.</summary>
        public static DepartmentSection? ParseOrNull(string raw)
        {
            var text = (raw ?? string.Empty).Trim().Replace(" ", string.Empty).Replace("_", string.Empty);
            if (text.Length == 0)
            {
                return null;
            }

            if (string.Equals(text, "Prep", StringComparison.OrdinalIgnoreCase) || text == "1")
            {
                return DepartmentSection.Prep;
            }

            if (string.Equals(text, "Senior", StringComparison.OrdinalIgnoreCase)
                || string.Equals(text, "SeniorSchool", StringComparison.OrdinalIgnoreCase)
                || text == "0")
            {
                return DepartmentSection.SeniorSchool;
            }

            return null;
        }

        /// <summary>Value used in query strings and form posts (enum name).</summary>
        public static string ToQueryValue(DepartmentSection section)
        {
            return section == DepartmentSection.Prep ? "Prep" : "SeniorSchool";
        }

        /// <summary>The grade number in a G## code (G00 = 0, G05 = 5); null for any other code.</summary>
        public static int? ParseGradeNumber(string code)
        {
            var match = GradeCodeRegex.Match((code ?? string.Empty).Trim());
            return match.Success
                ? (int?)int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
                : null;
        }

        /// <summary>Explicit sort number, else the number in the G## code, else last (int.MaxValue).</summary>
        public static int EffectiveSortNumber(int? sortOrder, string code)
        {
            if (sortOrder.HasValue)
            {
                return sortOrder.Value;
            }

            var parsed = ParseGradeNumber(code);
            return parsed.HasValue ? parsed.Value : int.MaxValue;
        }

        /// <summary>
        /// Report Section filter: the department ids a report should use. With explicit ids ticked, only those that are in the
        /// Section; with none ticked, every department of the Section. Empty result = the single sentinel id -1 (no department has
        /// Id -1), because an empty id list means "all departments" to every report builder.
        /// </summary>
        public static List<int> IntersectForReport(IEnumerable<int> sectionDepartmentIds, IList<int> requestedIds)
        {
            var inSection = (sectionDepartmentIds ?? new int[0]).Distinct().ToList();
            var effective = requestedIds != null && requestedIds.Count > 0
                ? requestedIds.Where(inSection.Contains).Distinct().ToList()
                : inSection;
            if (effective.Count == 0)
            {
                effective = new List<int> { -1 };
            }

            return effective;
        }

        public static IList<KeyValuePair<DepartmentSection, string>> Options()
        {
            return new List<KeyValuePair<DepartmentSection, string>>
            {
                new KeyValuePair<DepartmentSection, string>(DepartmentSection.Prep, PrepLabel),
                new KeyValuePair<DepartmentSection, string>(DepartmentSection.SeniorSchool, SeniorLabel)
            };
        }
    }
}
