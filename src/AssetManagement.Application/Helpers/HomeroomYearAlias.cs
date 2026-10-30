using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Display only (Allan 2026-10-30, pt547u: "7A, 7B are the rooms being linked to year 7. Year 7 itself is not a room").
    /// The class-to-room conversion keeps rooms 7A..7D under a sub-department coded ACADEMIC-GRADE7HR
    /// ("Grade 7 homerooms") under Academics. Wherever the app shows the hierarchy (Departments tree, breadcrumbs,
    /// pickers, move modal, requisition room list), such a group is shown as its matching Year: rooms appear directly
    /// under "Year 7" and the homerooms level is hidden. Data, approval routing (rooms still inherit from Academics)
    /// and asset tags are untouched; only labels, grouping and order change.
    /// A group is an alias only when its code is ACADEMIC-GRADE{N}HR and exactly one active top-level Year N exists
    /// in the same list ("Grade N" / "Year N" by name, else G0N by code). Anything else is shown as before.
    /// </summary>
    public static class HomeroomYearAlias
    {
        public const string PathSeparator = " > ";

        private static readonly Regex GroupCodePattern = new Regex(
            @"^\s*ACADEMIC-GRADE([0-9]{1,2})HR\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex YearNamePattern = new Regex(
            @"^\s*(?:grade|year)\s*([0-9]{1,2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex YearCodePattern = new Regex(
            @"^\s*G0*([0-9]{1,2})\s*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>Year number of a homerooms group code (ACADEMIC-GRADE7HR -> 7), else null.</summary>
        public static int? ParseGroupYearNumber(string code)
        {
            return ParseNumber(GroupCodePattern, code);
        }

        /// <summary>Year number of a Year department: "Grade 7" / "Year 7" by name, else "G07" by code.</summary>
        public static int? ParseYearNumber(string name, string code)
        {
            return ParseNumber(YearNamePattern, name) ?? ParseNumber(YearCodePattern, code);
        }

        public static bool LooksLikeGroup(DepartmentKind kind, string code)
        {
            return kind == DepartmentKind.SubDepartment && ParseGroupYearNumber(code).HasValue;
        }

        /// <summary>group id -> Year id for the alias groups in <paramref name="departments"/>.</summary>
        public static IDictionary<int, int> BuildMap(IEnumerable<DepartmentVm> departments)
        {
            return BuildMap(
                departments,
                x => x.Id,
                x => x.DepartmentKind,
                x => x.Name,
                x => x.Code,
                x => x.ParentDepartmentId,
                x => x.IsActive);
        }

        public static IDictionary<int, int> BuildMap<T>(
            IEnumerable<T> items,
            Func<T, int> id,
            Func<T, DepartmentKind> kind,
            Func<T, string> name,
            Func<T, string> code,
            Func<T, int?> parentId,
            Func<T, bool> isActive)
            where T : class
        {
            var list = (items ?? Enumerable.Empty<T>()).Where(x => x != null).ToList();
            var years = YearIdsByNumber(list, id, kind, name, code, parentId, isActive);
            var map = new Dictionary<int, int>();
            if (years.Count == 0)
            {
                return map;
            }

            foreach (var group in list.Where(x => kind(x) == DepartmentKind.SubDepartment))
            {
                var number = ParseGroupYearNumber(code(group));
                int yearId;
                if (number.HasValue && years.TryGetValue(number.Value, out yearId) && !map.ContainsKey(id(group)))
                {
                    map[id(group)] = yearId;
                }
            }

            return map;
        }

        /// <summary>Year number -> Year id, only for numbers with exactly one active top-level Year.</summary>
        public static IDictionary<int, int> YearIdsByNumber<T>(
            IEnumerable<T> items,
            Func<T, int> id,
            Func<T, DepartmentKind> kind,
            Func<T, string> name,
            Func<T, string> code,
            Func<T, int?> parentId,
            Func<T, bool> isActive)
            where T : class
        {
            var candidates = new Dictionary<int, HashSet<int>>();
            foreach (var year in (items ?? Enumerable.Empty<T>()).Where(x => x != null
                && kind(x) == DepartmentKind.Grade
                && !parentId(x).HasValue
                && isActive(x)))
            {
                var number = ParseYearNumber(name(year), code(year));
                if (!number.HasValue)
                {
                    continue;
                }

                HashSet<int> ids;
                if (!candidates.TryGetValue(number.Value, out ids))
                {
                    ids = new HashSet<int>();
                    candidates[number.Value] = ids;
                }

                ids.Add(id(year));
            }

            var result = new Dictionary<int, int>();
            foreach (var pair in candidates.Where(x => x.Value.Count == 1))
            {
                result[pair.Key] = pair.Value.First();
            }

            return result;
        }

        /// <summary>The parent to show: the Year for a room under an alias group, otherwise the stored parent.</summary>
        public static int? DisplayParentId(int? parentId, IDictionary<int, int> map)
        {
            int yearId;
            if (parentId.HasValue && map != null && map.TryGetValue(parentId.Value, out yearId))
            {
                return yearId;
            }

            return parentId;
        }

        public static bool IsAliasGroup(int departmentId, IDictionary<int, int> map)
        {
            return map != null && map.ContainsKey(departmentId);
        }

        /// <summary>"Year 7 > 7A" from the stored names ("Grade 7", "Grade 7A").</summary>
        public static string FormatRoomUnderYear(string yearName, string roomName)
        {
            var year = GradeDisplay.ToYear((yearName ?? string.Empty).Trim());
            var room = GradeDisplay.ToYear((roomName ?? string.Empty).Trim());
            if (year.Length == 0)
            {
                return room;
            }

            return room.Length == 0 ? year : year + PathSeparator + room;
        }

        /// <summary>
        /// Allan t550s2044: the Year whose page an alias group opens instead (Departments/Details of "Grade 7 homerooms"
        /// goes to Year 7). Null when the department is not an alias group.
        /// </summary>
        public static int? RedirectYearId(int departmentId, IEnumerable<DepartmentVm> departments)
        {
            int yearId;
            var map = BuildMap((departments ?? Enumerable.Empty<DepartmentVm>()).Where(x => x != null).ToList());
            return map.TryGetValue(departmentId, out yearId) ? yearId : (int?)null;
        }

        /// <summary>
        /// Year Details "Rooms" (Allan t550s2044): the Year's direct children plus the rooms of its alias groups
        /// (e.g. 7A-7D under "Grade 7 homerooms"), in natural order. The groups themselves are not listed.
        /// </summary>
        public static IList<DepartmentVm> RoomsForYear(int yearId, IEnumerable<DepartmentVm> departments)
        {
            var list = (departments ?? Enumerable.Empty<DepartmentVm>()).Where(x => x != null).ToList();
            var map = BuildMap(list);
            var groupIds = new HashSet<int>(map.Where(x => x.Value == yearId).Select(x => x.Key));
            return list
                .Where(x => x.ParentDepartmentId.HasValue
                    && x.Id != yearId
                    && !groupIds.Contains(x.Id)
                    && (x.ParentDepartmentId.Value == yearId || groupIds.Contains(x.ParentDepartmentId.Value)))
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .OrderBy(x => GradeDisplay.ToYear(x.Name ?? string.Empty), NaturalStringComparer.Instance)
                .ThenBy(x => x.Id)
                .ToList();
        }

        /// <summary>
        /// Departments tree: moves each alias group's rooms under its Year (after the Year's classes) and drops the
        /// group from its department's children. Call after Grade/Administrative children are attached.
        /// </summary>
        public static void AttachRoomsToYears(IList<DepartmentVm> departments, IEnumerable<DepartmentVm> administrativeItems)
        {
            var list = (departments ?? new List<DepartmentVm>()).Where(x => x != null).ToList();
            var map = BuildMap(list);
            if (map.Count == 0)
            {
                return;
            }

            var byId = new Dictionary<int, DepartmentVm>();
            foreach (var d in list)
            {
                if (!byId.ContainsKey(d.Id))
                {
                    byId[d.Id] = d;
                }
            }

            foreach (var pair in map)
            {
                DepartmentVm year;
                if (!byId.TryGetValue(pair.Value, out year))
                {
                    continue;
                }

                if (year.Children == null)
                {
                    year.Children = new List<DepartmentVm>();
                }

                var groupId = pair.Key;
                var rooms = list
                    .Where(x => x.ParentDepartmentId == groupId && x.DepartmentKind == DepartmentKind.Room)
                    .OrderBy(x => x.Name, NaturalStringComparer.Instance)
                    .ThenBy(x => x.Id)
                    .ToList();
                foreach (var room in rooms)
                {
                    if (!year.Children.Any(x => x.Id == room.Id))
                    {
                        year.Children.Add(room);
                    }
                }
            }

            foreach (var admin in administrativeItems ?? Enumerable.Empty<DepartmentVm>())
            {
                if (admin == null || admin.Children == null)
                {
                    continue;
                }

                var keep = admin.Children.Where(x => x == null || !map.ContainsKey(x.Id)).ToList();
                if (keep.Count != admin.Children.Count)
                {
                    admin.Children = keep;
                }
            }
        }

        private static int? ParseNumber(Regex pattern, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var match = pattern.Match(value);
            int number;
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out number))
            {
                return null;
            }

            return number;
        }
    }

    /// <summary>Case-insensitive, number-aware string order ("Year 2" before "Year 10"); see RequisitionTargetRules.NaturalCompare.</summary>
    public sealed class NaturalStringComparer : IComparer<string>
    {
        public static readonly NaturalStringComparer Instance = new NaturalStringComparer();

        public int Compare(string x, string y)
        {
            return RequisitionTargetRules.NaturalCompare(x, y);
        }
    }
}