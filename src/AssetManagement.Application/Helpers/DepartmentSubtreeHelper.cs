using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Cycle-safe subtree walks for parent-picker exclusion and save guards.
    /// </summary>
    public static class DepartmentSubtreeHelper
    {
        /// <summary>
        /// Returns rootId plus every descendant at any depth. Safe if ParentDepartmentId links form a loop.
        /// </summary>
        public static HashSet<int> GetSelfAndDescendantIds(
            int rootId,
            IEnumerable<Department> departments)
        {
            return GetSelfAndDescendantIds(
                rootId,
                (departments ?? Enumerable.Empty<Department>())
                    .Where(x => x != null)
                    .Select(x => new ParentLink(x.Id, x.ParentDepartmentId)));
        }

        public static HashSet<int> GetSelfAndDescendantIds(
            int rootId,
            IEnumerable<DepartmentVm> departments)
        {
            return GetSelfAndDescendantIds(
                rootId,
                (departments ?? Enumerable.Empty<DepartmentVm>())
                    .Where(x => x != null)
                    .Select(x => new ParentLink(x.Id, x.ParentDepartmentId)));
        }

        public static HashSet<int> GetSelfAndDescendantIds(
            int rootId,
            IEnumerable<ParentLink> links)
        {
            var byParent = (links ?? Enumerable.Empty<ParentLink>())
                .Where(x => x.ParentDepartmentId.HasValue)
                .GroupBy(x => x.ParentDepartmentId.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());

            var result = new HashSet<int> { rootId };
            var queue = new Queue<int>();
            queue.Enqueue(rootId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                List<int> children;
                if (!byParent.TryGetValue(current, out children))
                {
                    continue;
                }

                foreach (var childId in children)
                {
                    if (result.Add(childId))
                    {
                        queue.Enqueue(childId);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// True when parentId is missing/empty (allowed here), or when parentId is the unit itself or any descendant.
        /// </summary>
        public static bool IsSelfOrDescendantParent(
            int departmentId,
            int? parentDepartmentId,
            IEnumerable<Department> departments)
        {
            if (!parentDepartmentId.HasValue || parentDepartmentId.Value <= 0)
            {
                return false;
            }

            if (parentDepartmentId.Value == departmentId)
            {
                return true;
            }

            return GetSelfAndDescendantIds(departmentId, departments).Contains(parentDepartmentId.Value);
        }

        public static bool IsSelfOrDescendantParent(
            int departmentId,
            int? parentDepartmentId,
            IEnumerable<DepartmentVm> departments)
        {
            if (!parentDepartmentId.HasValue || parentDepartmentId.Value <= 0)
            {
                return false;
            }

            if (parentDepartmentId.Value == departmentId)
            {
                return true;
            }

            return GetSelfAndDescendantIds(departmentId, departments).Contains(parentDepartmentId.Value);
        }

        public struct ParentLink
        {
            public ParentLink(int id, int? parentDepartmentId)
            {
                Id = id;
                ParentDepartmentId = parentDepartmentId;
            }

            public int Id { get; private set; }
            public int? ParentDepartmentId { get; private set; }
        }
    }
}