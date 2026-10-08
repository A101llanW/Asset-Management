using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Guards Kind/Parent edits on units that have ACTIVE children: if the change would make a child
    /// invalid under <see cref="DepartmentHierarchyRules.AssertValidHierarchy"/>, the caller must supply a
    /// valid new parent for each such child (applied in the same save), otherwise the save is blocked.
    /// Uses live org data only (no names, codes or ids are hardcoded).
    /// </summary>
    public static class DepartmentKindChangeGuard
    {
        public sealed class ChildImpact
        {
            public Department Child { get; set; }

            public string Reason { get; set; }

            public IList<Department> CandidateParents { get; set; }

            public bool AllowNoParent { get; set; }
        }

        /// <summary>Returns the active direct children that would become invalid, with valid new-parent candidates.</summary>
        public static IList<ChildImpact> Analyze(
            int unitId,
            DepartmentKind newKind,
            int? newParentId,
            IEnumerable<Department> allDepartments)
        {
            var current = Simulate(allDepartments, int.MinValue, newKind, null, null);
            var simulated = Simulate(allDepartments, unitId, newKind, newParentId, null);
            var result = new List<ChildImpact>();
            var children = simulated.Values
                .Where(x => x.IsActive && x.Id != unitId && x.ParentDepartmentId == unitId)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ToList();
            foreach (var child in children)
            {
                var reason = Validate(child.Id, child.DepartmentKind, child.ParentDepartmentId, simulated);
                if (reason == null)
                {
                    continue;
                }

                // Only children this edit breaks; pre-existing (legacy) invalid placements never block a save.
                if (Validate(child.Id, child.DepartmentKind, child.ParentDepartmentId, current) != null)
                {
                    continue;
                }

                result.Add(new ChildImpact
                {
                    Child = child,
                    Reason = reason,
                    CandidateParents = GetCandidateParents(child, simulated),
                    AllowNoParent = Validate(child.Id, child.DepartmentKind, null, simulated) == null
                });
            }

            return result;
        }

        /// <summary>
        /// Server-side re-validation for the update path. Throws <see cref="BusinessException"/> listing the
        /// affected children when a required move is missing, a move is not for an active child of this unit,
        /// or the final tree (unit, moved children and all their active descendants) would still be invalid.
        /// </summary>
        public static void AssertChildrenRemainValid(
            int unitId,
            string unitName,
            DepartmentKind newKind,
            int? newParentId,
            IDictionary<int, int?> childMoves,
            IEnumerable<Department> allDepartments)
        {
            var list = Deduplicate(allDepartments);
            var moves = childMoves ?? new Dictionary<int, int?>();
            var impacts = Analyze(unitId, newKind, newParentId, list);
            var missing = impacts.Where(x => !moves.ContainsKey(x.Child.Id)).ToList();
            if (missing.Count > 0)
            {
                throw new BusinessException(BuildMissingMovesMessage(unitName, newKind, missing));
            }

            if (moves.Count == 0)
            {
                return;
            }

            var byId = list.ToDictionary(x => x.Id);
            foreach (var move in moves)
            {
                Department child;
                if (!byId.TryGetValue(move.Key, out child)
                    || !child.IsActive
                    || child.ParentDepartmentId != unitId
                    || child.Id == unitId)
                {
                    throw new BusinessException("Child move rejected: department #" + move.Key + " is not an active child of this unit.");
                }

                if (move.Value.HasValue && move.Value.Value == unitId)
                {
                    throw new BusinessException("'" + child.Name + "' must move to a different parent than the unit being changed.");
                }

                if (move.Value.HasValue && !byId.ContainsKey(move.Value.Value))
                {
                    throw new BusinessException("New parent for '" + child.Name + "' was not found in this organisation.");
                }

                if (move.Value.HasValue && !byId[move.Value.Value].IsActive)
                {
                    throw new BusinessException("New parent for '" + child.Name + "' is inactive.");
                }
            }

            var original = Simulate(list, int.MinValue, newKind, null, null);
            var final = Simulate(list, unitId, newKind, newParentId, moves);
            var toCheck = new HashSet<int> { unitId };
            foreach (var id in CollectDescendantIds(unitId, final))
            {
                toCheck.Add(id);
            }

            foreach (var movedId in moves.Keys)
            {
                toCheck.Add(movedId);
                foreach (var id in CollectDescendantIds(movedId, final))
                {
                    toCheck.Add(id);
                }
            }

            var errors = new List<string>();
            foreach (var id in toCheck.OrderBy(x => x))
            {
                Department node;
                if (!final.TryGetValue(id, out node) || (!node.IsActive && id != unitId))
                {
                    continue;
                }

                if (HasCycle(node, final))
                {
                    errors.Add(node.Name + ": would create a parent cycle");
                    continue;
                }

                var reason = Validate(node.Id, node.DepartmentKind, node.ParentDepartmentId, final);
                var mustBeValid = id == unitId
                    || moves.ContainsKey(id)
                    || Validate(id, original[id].DepartmentKind, original[id].ParentDepartmentId, original) == null;
                if (reason != null && mustBeValid)
                {
                    errors.Add(node.Name + " (" + DepartmentHierarchyRules.DisplayLabel(node.DepartmentKind) + "): " + reason);
                }
            }

            if (errors.Count > 0)
            {
                throw new BusinessException("The kind change and child moves would leave an invalid hierarchy: "
                    + string.Join("; ", errors) + ".");
            }
        }

        /// <summary>Posted moves -> childId => new parent (0 or null = no parent / independent).</summary>
        public static IDictionary<int, int?> NormalizeMoves(IEnumerable<DepartmentChildMoveVm> moves)
        {
            var result = new Dictionary<int, int?>();
            foreach (var move in moves ?? Enumerable.Empty<DepartmentChildMoveVm>())
            {
                if (move == null || move.ChildDepartmentId <= 0)
                {
                    continue;
                }

                if (!move.NewParentDepartmentId.HasValue)
                {
                    // Not chosen: leave it out so the guard reports the child as still needing a parent.
                    continue;
                }

                result[move.ChildDepartmentId] = move.NewParentDepartmentId.Value > 0
                    ? move.NewParentDepartmentId
                    : null;
            }

            return result;
        }

        public static string BuildMissingMovesMessage(string unitName, DepartmentKind newKind, IList<ChildImpact> missing)
        {
            var names = missing
                .Select(x => x.Child.Name + " (" + DepartmentHierarchyRules.DisplayLabel(x.Child.DepartmentKind) + ")")
                .ToList();
            return "Cannot save '" + (string.IsNullOrWhiteSpace(unitName) ? "this unit" : unitName)
                + "' as " + DepartmentHierarchyRules.DisplayLabel(newKind)
                + ": these active child units would become invalid - " + string.Join(", ", names)
                + ". Choose a new parent for each child in the 'Child units need a new parent' panel (or move them first), then save again.";
        }

        private static IList<Department> GetCandidateParents(Department child, IDictionary<int, Department> simulated)
        {
            var excluded = new HashSet<int>(CollectDescendantIds(child.Id, simulated)) { child.Id };
            return simulated.Values
                .Where(x => x.IsActive && !excluded.Contains(x.Id))
                .Where(x => Validate(child.Id, child.DepartmentKind, x.Id, simulated) == null)
                .Where(x => !HasCycle(x, simulated))
                .OrderBy(x => KindSortOrder(x.DepartmentKind))
                .ThenBy(x => x.Name)
                .ThenBy(x => x.Id)
                .ToList();
        }

        private static int KindSortOrder(DepartmentKind kind)
        {
            switch (kind)
            {
                case DepartmentKind.Administrative:
                    return 0;
                case DepartmentKind.SubDepartment:
                    return 1;
                case DepartmentKind.Grade:
                    return 2;
                default:
                    return 3;
            }
        }

        private static string Validate(int id, DepartmentKind kind, int? parentId, IDictionary<int, Department> state)
        {
            try
            {
                DepartmentHierarchyRules.AssertValidHierarchy(id, kind, parentId, state.Values);
                return null;
            }
            catch (BusinessException ex)
            {
                return ex.Message;
            }
        }

        private static bool HasCycle(Department node, IDictionary<int, Department> state)
        {
            var visited = new HashSet<int> { node.Id };
            var parentId = node.ParentDepartmentId;
            var guard = 0;
            while (parentId.HasValue && guard < 100)
            {
                if (!visited.Add(parentId.Value))
                {
                    return true;
                }

                Department parent;
                if (!state.TryGetValue(parentId.Value, out parent))
                {
                    return false;
                }

                parentId = parent.ParentDepartmentId;
                guard++;
            }

            return false;
        }

        private static IList<int> CollectDescendantIds(int rootId, IDictionary<int, Department> state)
        {
            var byParent = state.Values
                .Where(x => x.ParentDepartmentId.HasValue)
                .GroupBy(x => x.ParentDepartmentId.Value)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Id).ToList());
            var result = new List<int>();
            var seen = new HashSet<int> { rootId };
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
                    if (seen.Add(childId))
                    {
                        result.Add(childId);
                        queue.Enqueue(childId);
                    }
                }
            }

            return result;
        }

        private static IList<Department> Deduplicate(IEnumerable<Department> departments)
        {
            return (departments ?? Enumerable.Empty<Department>())
                .Where(x => x != null)
                .GroupBy(x => x.Id)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>Lightweight copies with the unit's proposed kind/parent and any child moves applied.</summary>
        private static IDictionary<int, Department> Simulate(
            IEnumerable<Department> allDepartments,
            int unitId,
            DepartmentKind newKind,
            int? newParentId,
            IDictionary<int, int?> moves)
        {
            var result = new Dictionary<int, Department>();
            foreach (var source in Deduplicate(allDepartments))
            {
                var copy = new Department
                {
                    Id = source.Id,
                    OrganizationId = source.OrganizationId,
                    Name = source.Name,
                    Code = source.Code,
                    ParentDepartmentId = source.ParentDepartmentId,
                    DepartmentKind = source.DepartmentKind,
                    IsActive = source.IsActive
                };
                if (copy.Id == unitId)
                {
                    copy.DepartmentKind = newKind;
                    copy.ParentDepartmentId = newParentId;
                }

                int? movedParent;
                if (moves != null && moves.TryGetValue(copy.Id, out movedParent))
                {
                    copy.ParentDepartmentId = movedParent;
                }

                result[copy.Id] = copy;
            }

            return result;
        }
    }
}
