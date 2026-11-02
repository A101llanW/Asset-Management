using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Security
{
    public enum RequisitionTargetScopeMode
    {
        /// <summary>No department and no Purchases.CreateForAnyDepartment: cannot raise (Allan 2026-11-02, answer 1).</summary>
        Blocked = 0,

        /// <summary>Purchases.CreateForAnyDepartment or the tenant administrator: any valid room of the organisation.</summary>
        OrganizationWide = 1,

        /// <summary>A department user: their own department and any valid room under it (answer 2).</summary>
        DepartmentSubtree = 2
    }

    /// <summary>
    /// The one requisition target scope rule (Allan 2026-11-02, answers 1 and 2), shared by the "A room" list, the
    /// "A person" list (the person's room, else their profile department), the approval-path preview and the server
    /// check (DepartmentScopeService.EnsureCanCreateForRequisitionTarget):
    ///   - Purchases.CreateForAnyDepartment or the tenant administrator (Company Admin / support impersonation):
    ///     organisation-wide;
    ///   - otherwise a user with a profile department: that department and every active unit below it (any depth);
    ///   - otherwise (no department): blocked, with <see cref="BlockedMessage"/>.
    /// Tagging an asset is not limited by this scope (answer 3: tags are categorisation data only).
    /// </summary>
    public sealed class RequisitionTargetScope
    {
        public const string BlockedMessage = "You can't raise requisitions yet because your profile has no department. Ask your Company Admin to assign you a department.";

        public const string OutsideScopeMessage = "This room is outside your scope. You can raise requisitions for your own department and the rooms under it.";

        private readonly HashSet<int> _departmentIds;

        private RequisitionTargetScope(RequisitionTargetScopeMode mode, int? rootDepartmentId, IEnumerable<int> departmentIds)
        {
            Mode = mode;
            RootDepartmentId = rootDepartmentId;
            _departmentIds = new HashSet<int>(departmentIds ?? Enumerable.Empty<int>());
        }

        public RequisitionTargetScopeMode Mode { get; private set; }

        /// <summary>The requester's own (profile) department for <see cref="RequisitionTargetScopeMode.DepartmentSubtree"/>.</summary>
        public int? RootDepartmentId { get; private set; }

        public bool IsBlocked
        {
            get { return Mode == RequisitionTargetScopeMode.Blocked; }
        }

        public bool IsOrganizationWide
        {
            get { return Mode == RequisitionTargetScopeMode.OrganizationWide; }
        }

        /// <summary>Own department + active descendants (empty unless DepartmentSubtree).</summary>
        public ICollection<int> DepartmentIds
        {
            get { return _departmentIds; }
        }

        public static RequisitionTargetScope OrganizationWideScope()
        {
            return new RequisitionTargetScope(RequisitionTargetScopeMode.OrganizationWide, null, null);
        }

        public static RequisitionTargetScope BlockedScope()
        {
            return new RequisitionTargetScope(RequisitionTargetScopeMode.Blocked, null, null);
        }

        /// <param name="activeDepartments">Active departments of the organisation as (id, parent id).</param>
        public static RequisitionTargetScope Resolve(
            bool canCreateForAnyDepartment,
            bool isTenantAdministrator,
            int? profileDepartmentId,
            IEnumerable<KeyValuePair<int, int?>> activeDepartments)
        {
            if (canCreateForAnyDepartment || isTenantAdministrator)
            {
                return OrganizationWideScope();
            }

            if (!profileDepartmentId.HasValue || profileDepartmentId.Value <= 0)
            {
                return BlockedScope();
            }

            return new RequisitionTargetScope(
                RequisitionTargetScopeMode.DepartmentSubtree,
                profileDepartmentId.Value,
                SelfAndActiveDescendants(profileDepartmentId.Value, activeDepartments));
        }

        public static RequisitionTargetScope Resolve(bool canCreateForAnyDepartment, bool isTenantAdministrator, int? profileDepartmentId, IEnumerable<Department> departments)
        {
            return Resolve(canCreateForAnyDepartment, isTenantAdministrator, profileDepartmentId, ToParentPairs(departments));
        }

        public static RequisitionTargetScope Resolve(bool canCreateForAnyDepartment, bool isTenantAdministrator, int? profileDepartmentId, IEnumerable<DepartmentVm> departments)
        {
            return Resolve(
                canCreateForAnyDepartment,
                isTenantAdministrator,
                profileDepartmentId,
                (departments ?? Enumerable.Empty<DepartmentVm>())
                    .Where(x => x != null && x.IsActive)
                    .Select(x => new KeyValuePair<int, int?>(x.Id, x.ParentDepartmentId)));
        }

        public static IEnumerable<KeyValuePair<int, int?>> ToParentPairs(IEnumerable<Department> departments)
        {
            return (departments ?? Enumerable.Empty<Department>())
                .Where(x => x != null && x.IsActive)
                .Select(x => new KeyValuePair<int, int?>(x.Id, x.ParentDepartmentId))
                .ToList();
        }

        /// <summary>The root plus every active unit below it (an inactive unit cuts its branch). Cycle-safe.</summary>
        public static IList<int> SelfAndActiveDescendants(int rootId, IEnumerable<KeyValuePair<int, int?>> activeDepartments)
        {
            var childrenByParent = new Dictionary<int, List<int>>();
            foreach (var pair in activeDepartments ?? Enumerable.Empty<KeyValuePair<int, int?>>())
            {
                if (!pair.Value.HasValue)
                {
                    continue;
                }

                List<int> children;
                if (!childrenByParent.TryGetValue(pair.Value.Value, out children))
                {
                    children = new List<int>();
                    childrenByParent[pair.Value.Value] = children;
                }

                children.Add(pair.Key);
            }

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

        /// <summary>The department may be the approval / delivery department of a requisition raised by this user.</summary>
        public bool Contains(int departmentId)
        {
            switch (Mode)
            {
                case RequisitionTargetScopeMode.OrganizationWide:
                    return true;
                case RequisitionTargetScopeMode.DepartmentSubtree:
                    return _departmentIds.Contains(departmentId);
                default:
                    return false;
            }
        }

        public bool IsOwnDepartment(int departmentId)
        {
            return Mode == RequisitionTargetScopeMode.DepartmentSubtree
                && RootDepartmentId.HasValue
                && RootDepartmentId.Value == departmentId;
        }

        /// <summary>
        /// "A room" option: a valid room / classroom in scope, or (department users) their own department when it is
        /// an active requisition target of any kind (that is what they raised for before 2026-11-02).
        /// </summary>
        public bool AllowsRoomOption(DepartmentVm department)
        {
            if (department == null || !Contains(department.Id))
            {
                return false;
            }

            return RequisitionTargetRules.IsValidRoomTarget(department)
                || (IsOwnDepartment(department.Id) && department.IsActive && department.IsRequisitionTarget);
        }

        public bool AllowsRoomOption(Department department, int? organizationId)
        {
            if (department == null || !Contains(department.Id))
            {
                return false;
            }

            return RequisitionTargetRules.IsValidRoomTarget(department, organizationId)
                || (IsOwnDepartment(department.Id) && department.IsActive && department.IsRequisitionTarget);
        }

        /// <summary>Ids of the "A room" options this user may pick from the given (active) departments.</summary>
        public ISet<int> AllowedRoomOptionIds(IEnumerable<DepartmentVm> departments)
        {
            return new HashSet<int>((departments ?? Enumerable.Empty<DepartmentVm>())
                .Where(AllowsRoomOption)
                .Select(x => x.Id));
        }

        /// <summary>
        /// A department user whose scope offers nothing but their own department keeps the read-only "your department"
        /// box (as before 2026-11-02); with rooms below it they pick from the scoped room list instead.
        /// </summary>
        public bool LocksToOwnDepartment(IEnumerable<DepartmentVm> departments)
        {
            if (Mode != RequisitionTargetScopeMode.DepartmentSubtree || !RootDepartmentId.HasValue)
            {
                return false;
            }

            return !AllowedRoomOptionIds(departments).Any(id => id != RootDepartmentId.Value);
        }

        /// <summary>The "A person" list limited to people whose room / department is in scope.</summary>
        public IList<RequisitionPersonTargetOptionVm> FilterPersonOptions(IEnumerable<RequisitionPersonTargetOptionVm> options)
        {
            var list = (options ?? Enumerable.Empty<RequisitionPersonTargetOptionVm>()).Where(x => x != null);
            if (IsOrganizationWide)
            {
                return list.ToList();
            }

            return list.Where(x => AllowsPersonRouting(x.RoutingDepartmentId)).ToList();
        }

        /// <summary>"A person" option: the person's routing department (their room, else profile department) is in scope.</summary>
        public bool AllowsPersonRouting(int? routingDepartmentId)
        {
            return routingDepartmentId.HasValue && Contains(routingDepartmentId.Value);
        }

        /// <summary>Server check used by DepartmentScopeService (the UI lists are not the only check).</summary>
        public void EnsureContains(Department department)
        {
            if (IsBlocked)
            {
                throw new BusinessException(BlockedMessage);
            }

            if (department == null || !Contains(department.Id))
            {
                throw new BusinessException(OutsideScopeMessage);
            }
        }
    }
}
