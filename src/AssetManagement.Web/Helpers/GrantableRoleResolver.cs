using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Web.Helpers
{
    /// <summary>A grantable role plus its department (null = organization-wide).</summary>
    public class GrantableRoleOption
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int? DepartmentId { get; set; }
    }

    /// <summary>
    /// Roles an actor may grant (Invite user and manual Create user, rel1009g; logic moved unchanged from
    /// UserInvitationsController, 214b7cd/d3e21a1): this organization's active roles only (never platform roles),
    /// only roles whose every permission the actor already has (no escalation), filtered by the
    /// Department -> Role cascade (Roles.DepartmentId, migration 096).
    /// </summary>
    public class GrantableRoleResolver
    {
        public const string CompanyAdminRoleName = "Company Admin";
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthorizationService _authorization;
        private readonly IUserService _userService;
        private readonly RoleDepartmentScopeRepository _roleDepartments;
        private readonly string _actorUserId;
        private readonly Func<bool> _organizationHasDepartments;

        public GrantableRoleResolver(
            IUnitOfWork unitOfWork,
            IAuthorizationService authorization,
            IUserService userService,
            RoleDepartmentScopeRepository roleDepartments,
            string actorUserId,
            Func<bool> organizationHasDepartments)
        {
            _unitOfWork = unitOfWork;
            _authorization = authorization;
            _userService = userService;
            _roleDepartments = roleDepartments;
            _actorUserId = actorUserId;
            _organizationHasDepartments = organizationHasDepartments ?? (() => false);
        }

        public static bool IsCompanyAdminRole(Role role)
        {
            return role != null && role.IsSystemRole
                && string.Equals(role.Name, CompanyAdminRoleName, StringComparison.OrdinalIgnoreCase);
        }

        public IList<Role> GetGrantableRoles(int organizationId)
        {
            var roles = _unitOfWork.Repository<Role>().GetAll()
                .Where(r => r.OrganizationId.HasValue && r.OrganizationId.Value == organizationId && r.IsActive)
                .OrderBy(r => r.Name)
                .ToList();
            if (roles.Count == 0)
            {
                return roles;
            }

            var userId = _actorUserId;
            var auth = _authorization;
            var roleIds = roles.Select(r => r.Id).ToList();
            var rolePermissions = _unitOfWork.Repository<RolePermission>().GetAll()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .ToList();
            var codes = _unitOfWork.Repository<Permission>().GetAll()
                .ToDictionary(p => p.Id, p => p.Code);
            // The actor's own role permissions (by id) plus anything the authorization service grants them.
            var actor = string.IsNullOrWhiteSpace(userId) || _userService == null ? null : _userService.GetById(userId);
            var ownPermissionIds = new HashSet<int>();
            if (actor != null && actor.RoleId.HasValue)
            {
                var ownRoleId = actor.RoleId.Value;
                foreach (var rp in _unitOfWork.Repository<RolePermission>().GetAll().Where(x => x.RoleId == ownRoleId))
                {
                    ownPermissionIds.Add(rp.PermissionId);
                }
            }

            var held = new Dictionary<int, bool>();
            Func<int, bool> has = permissionId =>
            {
                bool value;
                if (!held.TryGetValue(permissionId, out value))
                {
                    string code;
                    value = ownPermissionIds.Contains(permissionId)
                        || (codes.TryGetValue(permissionId, out code) && !string.IsNullOrWhiteSpace(code)
                            && !string.IsNullOrWhiteSpace(userId) && auth != null && auth.HasPermission(userId, code));
                    held[permissionId] = value;
                }

                return value;
            };

            return roles
                .Where(r => rolePermissions.Where(rp => rp.RoleId == r.Id).All(rp => has(rp.PermissionId)))
                .ToList();
        }

        /// <summary>
        /// Grantable roles that apply in the department: organization-wide roles for every department, a department
        /// role only for its own department. With no department chosen, only Company Admin (or, when the
        /// organization has no departments yet, every organization-wide role).
        /// </summary>
        public IList<GrantableRoleOption> GetRolesForDepartment(int organizationId, int? departmentId)
        {
            var roleDepartments = _roleDepartments == null
                ? new Dictionary<int, int?>()
                : _roleDepartments.GetRoleDepartmentIds(organizationId);
            var noDepartmentAllowsAll = !departmentId.HasValue && !_organizationHasDepartments();
            return GetGrantableRoles(organizationId)
                .Select(r =>
                {
                    int? roleDepartmentId;
                    if (!roleDepartments.TryGetValue(r.Id, out roleDepartmentId))
                    {
                        roleDepartmentId = null;
                    }

                    return new { Role = r, DepartmentId = roleDepartmentId };
                })
                .Where(x => departmentId.HasValue
                    ? (!x.DepartmentId.HasValue || x.DepartmentId.Value == departmentId.Value)
                    : (!x.DepartmentId.HasValue && (noDepartmentAllowsAll || IsCompanyAdminRole(x.Role))))
                .Select(x => new GrantableRoleOption { Id = x.Role.Id, Name = x.Role.Name, DepartmentId = x.DepartmentId })
                .ToList();
        }

        /// <summary>
        /// Same checks as the Invite POST. Returns null when valid, otherwise (field, message).
        /// </summary>
        public KeyValuePair<string, string>? ValidateRoleChoice(
            int organizationId,
            int? roleId,
            int? departmentId,
            IEnumerable<DepartmentVm> allowedDepartments,
            string noRoleMessage)
        {
            if (departmentId.HasValue && !(allowedDepartments ?? Enumerable.Empty<DepartmentVm>()).Any(d => d.Id == departmentId.Value))
            {
                return new KeyValuePair<string, string>("DepartmentId", "You can only choose an active department within your scope.");
            }

            if (!roleId.HasValue)
            {
                return new KeyValuePair<string, string>("RoleId", noRoleMessage ?? "Select a role.");
            }

            var grantable = GetGrantableRoles(organizationId).FirstOrDefault(r => r.Id == roleId.Value);
            if (grantable == null)
            {
                return new KeyValuePair<string, string>("RoleId", "You cannot grant the selected role. Choose a role whose permissions you already have.");
            }

            if (!departmentId.HasValue && _organizationHasDepartments() && !IsCompanyAdminRole(grantable))
            {
                return new KeyValuePair<string, string>("DepartmentId", "Select a department. Only Company Admin can be set up without a department.");
            }

            if (!GetRolesForDepartment(organizationId, departmentId).Any(r => r.Id == roleId.Value))
            {
                return new KeyValuePair<string, string>("RoleId", "The selected role does not apply in the selected department.");
            }

            return null;
        }
    }
}
