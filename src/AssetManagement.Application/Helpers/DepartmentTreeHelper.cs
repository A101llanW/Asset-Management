using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// rel1009g: generic (any depth) department tree helpers driven by ParentDepartmentId: "Parent &gt; Child" path
    /// labels, subtree ids, ancestor checks, requisition options in tree order, asset option labels and the
    /// Class/Room person in charge. Cycle-safe (a visited set / depth cap) so bad parent data cannot loop.
    /// </summary>
    public static class DepartmentTreeHelper
    {
        public const string PathSeparator = " > ";
        private const int MaxDepth = 32;

        public static string BuildPathLabel(int departmentId, IEnumerable<DepartmentVm> departments)
        {
            return BuildPathLabel(departmentId, ToLookup(departments));
        }

        public static string BuildPathLabel(int departmentId, IDictionary<int, DepartmentVm> lookup)
        {
            return lookup == null ? string.Empty : BuildPathLabel(departmentId, lookup, HomeroomYearAlias.BuildMap(lookup.Values));
        }

        /// <summary>
        /// Same, with the homerooms alias map (pt547u, display only): a room under "Grade 7 homerooms" reads
        /// "Grade 7 &gt; Grade 7A" (callers apply GradeDisplay: "Year 7 &gt; 7A"); the homerooms level is skipped.
        /// </summary>
        public static string BuildPathLabel(int departmentId, IDictionary<int, DepartmentVm> lookup, IDictionary<int, int> aliasMap)
        {
            if (lookup == null)
            {
                return string.Empty;
            }

            var names = new List<string>();
            var seen = new HashSet<int>();
            int? current = departmentId;
            while (current.HasValue && seen.Add(current.Value) && names.Count < MaxDepth)
            {
                DepartmentVm node;
                if (!lookup.TryGetValue(current.Value, out node) || node == null)
                {
                    break;
                }

                int yearId;
                if (node.Id != departmentId
                    && aliasMap != null
                    && aliasMap.TryGetValue(node.Id, out yearId)
                    && lookup.ContainsKey(yearId))
                {
                    current = yearId;
                    continue;
                }

                names.Insert(0, (node.Name ?? string.Empty).Trim());
                current = node.ParentDepartmentId;
            }

            return string.Join(PathSeparator, names.Where(x => x.Length > 0));
        }

        /// <summary>
        /// Breadcrumb trail above a department: its ancestors root first (the department itself is not included),
        /// labelled for display with <see cref="GradeDisplay"/> ("Grade 6" -> "Year 6", "Grade 6A" -> "6A").
        /// Only ancestors present in <paramref name="lookup"/> (the user's scoped list) are returned; the walk stops
        /// at the first missing parent. Cycle-safe.
        /// </summary>
        public static IList<KeyValuePair<int, string>> GetAncestorTrail(DepartmentVm department, IDictionary<int, DepartmentVm> lookup)
        {
            var trail = new List<KeyValuePair<int, string>>();
            if (department == null || lookup == null)
            {
                return trail;
            }

            // pt547u (display only): a room under "Grade 7 homerooms" shows Departments > Year 7 > 7A.
            var aliasMap = HomeroomYearAlias.BuildMap(lookup.Values);
            var seen = new HashSet<int> { department.Id };
            var current = department.ParentDepartmentId;
            while (current.HasValue && seen.Add(current.Value) && trail.Count < MaxDepth)
            {
                DepartmentVm node;
                if (!lookup.TryGetValue(current.Value, out node) || node == null)
                {
                    break;
                }

                int yearId;
                if (aliasMap.TryGetValue(node.Id, out yearId) && lookup.ContainsKey(yearId))
                {
                    current = yearId;
                    continue;
                }

                var label = GradeDisplay.ToYear((node.Name ?? string.Empty).Trim());
                trail.Insert(0, new KeyValuePair<int, string>(node.Id, label.Length == 0 ? "Unit #" + node.Id : label));
                current = node.ParentDepartmentId;
            }

            return trail;
        }

        /// <summary>Same as <see cref="GetAncestorTrail(DepartmentVm, IDictionary{int, DepartmentVm})"/> over a list.</summary>
        public static IList<KeyValuePair<int, string>> GetAncestorTrail(DepartmentVm department, IEnumerable<DepartmentVm> departments)
        {
            return GetAncestorTrail(department, ToLookup(departments));
        }

        /// <summary>The root id plus every descendant id (breadth first, root first).</summary>
        public static IList<int> GetSelfAndDescendantIds(int rootId, IEnumerable<DepartmentVm> departments)
        {
            var list = (departments ?? Enumerable.Empty<DepartmentVm>()).Where(x => x != null).ToList();
            var childrenByParent = list
                .Where(x => x.ParentDepartmentId.HasValue)
                .GroupBy(x => x.ParentDepartmentId.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
            var result = new List<int>();
            var seen = new HashSet<int>();
            var queue = new Queue<int>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (!seen.Add(id))
                {
                    continue;
                }

                result.Add(id);
                List<int> children;
                if (childrenByParent.TryGetValue(id, out children))
                {
                    foreach (var child in children)
                    {
                        queue.Enqueue(child);
                    }
                }
            }

            return result;
        }

        /// <summary>True when candidate == root or root is an ancestor of candidate (parent chain walk).</summary>
        public static bool IsSelfOrDescendant(int? candidateId, int rootId, IDictionary<int, int?> parentById)
        {
            if (!candidateId.HasValue)
            {
                return false;
            }

            var seen = new HashSet<int>();
            int? current = candidateId;
            while (current.HasValue && seen.Add(current.Value) && seen.Count <= MaxDepth)
            {
                if (current.Value == rootId)
                {
                    return true;
                }

                int? parent;
                if (parentById == null || !parentById.TryGetValue(current.Value, out parent))
                {
                    return false;
                }

                current = parent;
            }

            return false;
        }

        /// <summary>
        /// Requisition targets in tree order at any depth: Prep section first, then Senior school; within a section
        /// Grades (by sort number, then code), then Administrative, then other roots;
        /// children by code/name under their parent. A non-target parent is skipped but its children are still walked.
        /// </summary>
        public static IList<KeyValuePair<int, string>> BuildRequisitionOptions(IEnumerable<DepartmentVm> departments)
        {
            return BuildTreeOptions(departments, true);
        }

        /// <summary>
        /// Same tree order and "Parent > Child" labels as BuildRequisitionOptions; when requisitionTargetsOnly is false
        /// every active department is listed (Asset Requests department picker).
        /// </summary>
        public static IList<KeyValuePair<int, string>> BuildTreeOptions(IEnumerable<DepartmentVm> departments, bool requisitionTargetsOnly)
        {
            var all = (departments ?? Enumerable.Empty<DepartmentVm>()).Where(x => x != null && x.IsActive).ToList();
            var lookup = ToLookup(all);
            // pt547u (display only): homerooms groups are hidden and their rooms listed under the matching Year.
            var aliasMap = HomeroomYearAlias.BuildMap(all);
            var list = all.Where(x => !aliasMap.ContainsKey(x.Id)).ToList();
            Func<DepartmentVm, int?> displayParent = x => HomeroomYearAlias.DisplayParentId(x.ParentDepartmentId, aliasMap);
            var childrenByParent = list
                .Where(x => displayParent(x).HasValue && lookup.ContainsKey(displayParent(x).Value))
                .GroupBy(x => displayParent(x).Value)
                .ToDictionary(g => g.Key, g => SortSiblings(g).ToList());
            var roots = list
                .Where(x => !displayParent(x).HasValue || !lookup.ContainsKey(displayParent(x).Value))
                .OrderBy(x => DepartmentSectionHelper.Rank(x.Section))
                .ThenBy(x => RootRank(x.DepartmentKind))
                .ThenBy(x => x.DepartmentKind == DepartmentKind.Grade ? DepartmentSectionHelper.EffectiveSortNumber(x.SortOrder, x.Code) : 0)
                .ThenBy(x => x.DepartmentKind == DepartmentKind.Grade ? (x.Code ?? x.Name) : x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var result = new List<KeyValuePair<int, string>>();
            var seen = new HashSet<int>();
            foreach (var root in roots)
            {
                Walk(root, childrenByParent, lookup, aliasMap, result, seen, 0, requisitionTargetsOnly);
            }

            return result;
        }

        /// <summary>Asset option text: "tag - serial - name (Department)"; empty parts are skipped.</summary>
        public static string FormatAssetOption(string assetTag, string serialNumber, string assetName, string departmentName)
        {
            var parts = new[] { assetTag, serialNumber, assetName }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
            var text = string.Join(" - ", parts);
            if (!string.IsNullOrWhiteSpace(departmentName))
            {
                text = (text + " (" + departmentName.Trim() + ")").Trim();
            }

            return text;
        }

        /// <summary>Class -> class teacher, Room -> room custodian, otherwise null (same rule as Assignments).</summary>
        public static string ResolvePicUserId(DepartmentVm department)
        {
            if (department == null || !department.IsActive)
            {
                return null;
            }

            if (department.DepartmentKind == DepartmentKind.Class && !string.IsNullOrWhiteSpace(department.ClassTeacherUserId))
            {
                return department.ClassTeacherUserId.Trim();
            }

            if (department.DepartmentKind == DepartmentKind.Room && !string.IsNullOrWhiteSpace(department.RoomCustodianUserId))
            {
                return department.RoomCustodianUserId.Trim();
            }

            return null;
        }

        public static bool IsRoomTargetKind(DepartmentKind kind)
        {
            return kind == DepartmentKind.Room || kind == DepartmentKind.Class;
        }

        private static void Walk(
            DepartmentVm node,
            IDictionary<int, List<DepartmentVm>> childrenByParent,
            IDictionary<int, DepartmentVm> lookup,
            IDictionary<int, int> aliasMap,
            IList<KeyValuePair<int, string>> result,
            ISet<int> seen,
            int depth,
            bool requisitionTargetsOnly)
        {
            if (node == null || depth > MaxDepth || !seen.Add(node.Id))
            {
                return;
            }

            if (node.IsRequisitionTarget || !requisitionTargetsOnly)
            {
                result.Add(new KeyValuePair<int, string>(node.Id, BuildPathLabel(node.Id, lookup, aliasMap)));
            }

            List<DepartmentVm> children;
            if (childrenByParent.TryGetValue(node.Id, out children))
            {
                foreach (var child in children)
                {
                    Walk(child, childrenByParent, lookup, aliasMap, result, seen, depth + 1, requisitionTargetsOnly);
                }
            }
        }

        private static IEnumerable<DepartmentVm> SortSiblings(IEnumerable<DepartmentVm> siblings)
        {
            return siblings
                .OrderBy(x => string.IsNullOrWhiteSpace(x.Code) ? x.Name : x.Code, NaturalStringComparer.Instance)
                .ThenBy(x => x.Name, NaturalStringComparer.Instance);
        }

        private static int RootRank(DepartmentKind kind)
        {
            switch (kind)
            {
                case DepartmentKind.Grade:
                    return 0;
                case DepartmentKind.Administrative:
                    return 1;
                default:
                    return 2;
            }
        }

        private static IDictionary<int, DepartmentVm> ToLookup(IEnumerable<DepartmentVm> departments)
        {
            var lookup = new Dictionary<int, DepartmentVm>();
            foreach (var d in departments ?? Enumerable.Empty<DepartmentVm>())
            {
                if (d != null && !lookup.ContainsKey(d.Id))
                {
                    lookup[d.Id] = d;
                }
            }

            return lookup;
        }
    }
}
