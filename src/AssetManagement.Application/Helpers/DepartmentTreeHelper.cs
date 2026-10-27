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

                names.Insert(0, (node.Name ?? string.Empty).Trim());
                current = node.ParentDepartmentId;
            }

            return string.Join(PathSeparator, names.Where(x => x.Length > 0));
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
            var list = (departments ?? Enumerable.Empty<DepartmentVm>()).Where(x => x != null && x.IsActive).ToList();
            var lookup = ToLookup(list);
            var childrenByParent = list
                .Where(x => x.ParentDepartmentId.HasValue && lookup.ContainsKey(x.ParentDepartmentId.Value))
                .GroupBy(x => x.ParentDepartmentId.Value)
                .ToDictionary(g => g.Key, g => SortSiblings(g).ToList());
            var roots = list
                .Where(x => !x.ParentDepartmentId.HasValue || !lookup.ContainsKey(x.ParentDepartmentId.Value))
                .OrderBy(x => DepartmentSectionHelper.Rank(x.Section))
                .ThenBy(x => RootRank(x.DepartmentKind))
                .ThenBy(x => x.DepartmentKind == DepartmentKind.Grade ? DepartmentSectionHelper.EffectiveSortNumber(x.SortOrder, x.Code) : 0)
                .ThenBy(x => x.DepartmentKind == DepartmentKind.Grade ? (x.Code ?? x.Name) : x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var result = new List<KeyValuePair<int, string>>();
            var seen = new HashSet<int>();
            foreach (var root in roots)
            {
                Walk(root, childrenByParent, lookup, result, seen, 0, requisitionTargetsOnly);
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
                result.Add(new KeyValuePair<int, string>(node.Id, BuildPathLabel(node.Id, lookup)));
            }

            List<DepartmentVm> children;
            if (childrenByParent.TryGetValue(node.Id, out children))
            {
                foreach (var child in children)
                {
                    Walk(child, childrenByParent, lookup, result, seen, depth + 1, requisitionTargetsOnly);
                }
            }
        }

        private static IEnumerable<DepartmentVm> SortSiblings(IEnumerable<DepartmentVm> siblings)
        {
            return siblings
                .OrderBy(x => string.IsNullOrWhiteSpace(x.Code) ? x.Name : x.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase);
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
