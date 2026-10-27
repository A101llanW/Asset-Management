using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    /// <summary>How a stage's approver set was resolved (see <see cref="StageApproverScopeResolver"/>).</summary>
    public enum StageApproverScopeKind
    {
        /// <summary>No stage role and no named person: nobody.</summary>
        None = 0,

        /// <summary>The stage names a person: only that person (unchanged behaviour).</summary>
        NamedPerson = 1,

        /// <summary>Active role holders whose department is the request's department.</summary>
        Department = 2,

        /// <summary>Active role holders of the nearest ancestor (sub-department / department / grade) that has any.</summary>
        AncestorDepartment = 3,

        /// <summary>Active role holders with no department (org-wide approvers such as procurement or admins).</summary>
        NoDepartment = 4,

        /// <summary>Every active role holder in the organisation (legacy behaviour; last resort).</summary>
        Organization = 5,

        /// <summary>
        /// Owner decision (catalog requisition flows): a stage that comes from a catalog path (category / type /
        /// sub-type) resolves to the active holders of the stage role configured on that path FIRST, before the
        /// requesting department chain. Catalog nodes are organisation-wide (no owning department), so the holders
        /// scoped to the path are every active holder of that role in the organisation.
        /// </summary>
        CatalogPath = 6,

        /// <summary>
        /// Migration 107: the stage role is the Department Head role and the request's unit (or the nearest ancestor)
        /// has a head set for the unit's section (Prep / Senior school). Only that person, whatever their own role.
        /// </summary>
        SectionHead = 7
    }

    /// <summary>The resolved approvers of one approval stage (or PO-raiser / review step) for one request.</summary>
    public sealed class StageApproverScope
    {
        private readonly HashSet<string> _ids;

        public StageApproverScope(StageApproverScopeKind kind, int? resolvedDepartmentId, IEnumerable<string> userIds)
        {
            Kind = kind;
            ResolvedDepartmentId = resolvedDepartmentId;
            UserIds = (userIds ?? Enumerable.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _ids = new HashSet<string>(UserIds, StringComparer.OrdinalIgnoreCase);
        }

        public StageApproverScopeKind Kind { get; private set; }

        /// <summary>True when the approver was chosen as a section head: approval does not require holding the stage role.</summary>
        public bool BypassesRoleCheck
        {
            get { return Kind == StageApproverScopeKind.SectionHead; }
        }

        /// <summary>Department whose holders were chosen (Department / AncestorDepartment / SectionHead).</summary>
        public int? ResolvedDepartmentId { get; private set; }

        public IList<string> UserIds { get; private set; }

        public bool Contains(string userId)
        {
            return !string.IsNullOrWhiteSpace(userId) && _ids.Contains(userId.Trim());
        }

        public override string ToString()
        {
            return Kind + (ResolvedDepartmentId.HasValue ? ("(" + ResolvedDepartmentId.Value + ")") : string.Empty)
                + ":" + string.Join(",", UserIds.ToArray());
        }
    }

    /// <summary>
    /// ONE resolver for "who are the approvers of this stage for this request", shared by pending-approval
    /// notifications AND approval eligibility (approve/reject, pending lists, asset access, PO raiser) so the
    /// two can never drift. Rule (rel1009e):
    ///   1. A named person on the stage = only that person (unchanged).
    ///   2. Else active holders of the stage role whose DepartmentId is the request's department.
    ///   3. Else the nearest ancestor (ParentDepartmentId chain: room -> class -> grade -> department ...) with holders.
    ///   4. Else active holders with NO department (org-wide approvers, e.g. Procurement / Company Admin).
    ///   5. Else every active holder in the organisation (legacy org-wide behaviour).
    /// A request with no department resolves straight to step 5 (legacy behaviour).
    /// Holders assigned to an unrelated department are never chosen while steps 2-4 find anyone.
    /// </summary>
    public sealed class StageApproverScopeResolver
    {
        private const int MaxAncestorDepth = 32;

        private readonly Func<IEnumerable<UserVm>> _usersFactory;
        private readonly Func<int, Department> _departmentById;
        private readonly Func<int, string> _roleNameById;
        private readonly Dictionary<int, string> _roleNames = new Dictionary<int, string>();
        private IList<UserVm> _users;
        private readonly Dictionary<int, Department> _departments = new Dictionary<int, Department>();

        public StageApproverScopeResolver(Func<IEnumerable<UserVm>> usersFactory, Func<int, Department> departmentById)
            : this(usersFactory, departmentById, null)
        {
        }

        /// <param name="roleNameById">Role name lookup; when given, Department Head stages use section heads first (migration 107).</param>
        public StageApproverScopeResolver(Func<IEnumerable<UserVm>> usersFactory, Func<int, Department> departmentById, Func<int, string> roleNameById)
        {
            _usersFactory = usersFactory;
            _departmentById = departmentById;
            _roleNameById = roleNameById;
        }

        /// <summary>Resolver over the tenant-filtered user list (IUserService) and the unit of work's departments.</summary>
        public static StageApproverScopeResolver Create(IUnitOfWork unitOfWork, IUserService userService)
        {
            return new StageApproverScopeResolver(
                () => userService == null ? Enumerable.Empty<UserVm>() : userService.GetAll(),
                id => unitOfWork == null ? null : unitOfWork.Repository<Department>().GetById(id),
                id =>
                {
                    var role = unitOfWork == null ? null : unitOfWork.Repository<Role>().GetById(id);
                    return role == null ? null : role.Name;
                });
        }

        /// <summary>One-shot convenience for <see cref="Create"/> + <see cref="Resolve"/>.</summary>
        public static StageApproverScope ResolveFor(
            IUnitOfWork unitOfWork,
            IUserService userService,
            int? stageRoleId,
            string namedUserId,
            int? requestDepartmentId)
        {
            return Create(unitOfWork, userService).Resolve(stageRoleId, namedUserId, requestDepartmentId);
        }

        /// <summary>As <see cref="ResolveFor(IUnitOfWork, IUserService, int?, string, int?)"/>; when
        /// <paramref name="catalogPathFirst"/> is true the catalog path's role holders win before the department chain.</summary>
        public static StageApproverScope ResolveFor(
            IUnitOfWork unitOfWork,
            IUserService userService,
            int? stageRoleId,
            string namedUserId,
            int? requestDepartmentId,
            bool catalogPathFirst)
        {
            return Create(unitOfWork, userService).Resolve(stageRoleId, namedUserId, requestDepartmentId, catalogPathFirst);
        }

        public StageApproverScope Resolve(int? stageRoleId, string namedUserId, int? requestDepartmentId)
        {
            return Resolve(stageRoleId, namedUserId, requestDepartmentId, false);
        }

        /// <summary>
        /// Order: 1. named person; 2. (catalogPathFirst only) active holders of the catalog path's stage role;
        /// 3. requesting department; 4. nearest ancestor; 5. holders with no department; 6. org-wide.
        /// </summary>
        public StageApproverScope Resolve(int? stageRoleId, string namedUserId, int? requestDepartmentId, bool catalogPathFirst)
        {
            if (!string.IsNullOrWhiteSpace(namedUserId))
            {
                // rel1009g: a named person who has been deactivated no longer approves; fall back to the stage role.
                var named = namedUserId.Trim();
                var namedUser = GetUsers().FirstOrDefault(x => x != null && string.Equals(x.Id, named, StringComparison.OrdinalIgnoreCase));
                if (namedUser == null || namedUser.IsActive)
                {
                    return new StageApproverScope(StageApproverScopeKind.NamedPerson, null, new[] { named });
                }
            }

            if (!stageRoleId.HasValue || stageRoleId.Value <= 0)
            {
                return new StageApproverScope(StageApproverScopeKind.None, null, null);
            }

            var roleId = stageRoleId.Value;
            var holders = GetUsers()
                .Where(x => x != null && x.IsActive && x.RoleId == roleId && !string.IsNullOrWhiteSpace(x.Id))
                .ToList();

            // Migration 107: Department Head stage -> the section head (Prep / Senior) of the unit or nearest ancestor,
            // checked level by level BEFORE that level's role holders. No section head anywhere = old behaviour below.
            var useSectionHeads = requestDepartmentId.HasValue
                && requestDepartmentId.Value > 0
                && DepartmentHeadAssignmentRules.IsDepartmentHeadRoleName(GetRoleName(roleId));
            if (useSectionHeads)
            {
                var sectionScope = ResolveSectionHeadChain(requestDepartmentId.Value, holders);
                if (sectionScope != null)
                {
                    return sectionScope;
                }
            }

            if (catalogPathFirst && holders.Count > 0)
            {
                return new StageApproverScope(StageApproverScopeKind.CatalogPath, null, holders.Select(x => x.Id));
            }

            if (requestDepartmentId.HasValue && requestDepartmentId.Value > 0)
            {
                var visited = new HashSet<int>();
                int? current = requestDepartmentId.Value;
                var depth = 0;
                while (current.HasValue && current.Value > 0 && depth < MaxAncestorDepth && visited.Add(current.Value))
                {
                    var levelId = current.Value;
                    var atLevel = holders.Where(x => x.DepartmentId == levelId).Select(x => x.Id).ToList();
                    if (atLevel.Count > 0)
                    {
                        return new StageApproverScope(
                            depth == 0 ? StageApproverScopeKind.Department : StageApproverScopeKind.AncestorDepartment,
                            levelId,
                            atLevel);
                    }

                    var department = GetDepartment(levelId);
                    current = department == null ? null : department.ParentDepartmentId;
                    depth++;
                }

                var noDepartment = holders.Where(x => !x.DepartmentId.HasValue).Select(x => x.Id).ToList();
                if (noDepartment.Count > 0)
                {
                    return new StageApproverScope(StageApproverScopeKind.NoDepartment, null, noDepartment);
                }
            }

            return new StageApproverScope(StageApproverScopeKind.Organization, null, holders.Select(x => x.Id));
        }

        /// <summary>
        /// Walks unit -> ancestors. At each level: that level's section head (active user) wins; else that level's
        /// role holders. Returns null only when NO level has a section head (caller then runs the normal rule).
        /// </summary>
        private StageApproverScope ResolveSectionHeadChain(int requestDepartmentId, IList<UserVm> holders)
        {
            var start = GetDepartment(requestDepartmentId);
            if (start == null)
            {
                return null;
            }

            var section = DepartmentSectionHelper.Normalize(start.Section);
            var match = DepartmentSectionHeads.Resolve(requestDepartmentId, section, GetDepartment, IsActiveUser);
            if (match == null)
            {
                return null;
            }

            // A role holder placed on a level BELOW the section head's department is nearer and still wins.
            var visited = new HashSet<int>();
            int? current = requestDepartmentId;
            var depth = 0;
            while (current.HasValue && current.Value > 0 && depth < match.Depth && visited.Add(current.Value))
            {
                var levelId = current.Value;
                var atLevel = holders.Where(x => x.DepartmentId == levelId).Select(x => x.Id).ToList();
                if (atLevel.Count > 0)
                {
                    return new StageApproverScope(
                        depth == 0 ? StageApproverScopeKind.Department : StageApproverScopeKind.AncestorDepartment,
                        levelId,
                        atLevel);
                }

                var department = GetDepartment(levelId);
                current = department == null ? null : department.ParentDepartmentId;
                depth++;
            }

            return new StageApproverScope(StageApproverScopeKind.SectionHead, match.DepartmentId, new[] { match.UserId });
        }

        private bool IsActiveUser(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var id = userId.Trim();
            var user = GetUsers().FirstOrDefault(x => x != null && string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
            return user != null && user.IsActive;
        }

        private string GetRoleName(int roleId)
        {
            if (_roleNameById == null)
            {
                return null;
            }

            string name;
            if (!_roleNames.TryGetValue(roleId, out name))
            {
                name = _roleNameById(roleId);
                _roleNames[roleId] = name;
            }

            return name;
        }

        private IList<UserVm> GetUsers()
        {
            if (_users == null)
            {
                var source = _usersFactory == null ? null : _usersFactory();
                _users = source == null ? new List<UserVm>() : source.ToList();
            }

            return _users;
        }

        private Department GetDepartment(int id)
        {
            Department department;
            if (_departments.TryGetValue(id, out department))
            {
                return department;
            }

            department = _departmentById == null ? null : _departmentById(id);
            _departments[id] = department;
            return department;
        }
    }
}
