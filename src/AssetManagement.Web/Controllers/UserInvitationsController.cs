using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [AnyPermissionAuthorize("Users.Invite")]
    [ModuleAccess(ModulePermissionCatalog.Users)]
    public class UserInvitationsController : BaseController
    {
        private readonly IUserInvitationService _invitationService;
        private readonly IRoleService _roleService;
        private readonly IOrganizationScopeService _organizationScope;

        public UserInvitationsController()
        {
            _invitationService = DependencyResolver.Current.GetService<IUserInvitationService>();
            _roleService = BuildRoleService();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
        }

        public ActionResult Index(int page = 1, int pageSize = 10)
        {
            var organizationId = GetCurrentOrganizationIdOrDeny();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            var pageResult = _invitationService.GetListPage(organizationId.Value, page, pageSize);
            return View(ToListPage(pageResult));
        }

        public ActionResult Create(string returnUrl = null)
        {
            var organizationId = GetCurrentOrganizationIdOrDeny();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            ConfigureCreateViewBag(returnUrl, organizationId.Value, null, null);
            return View(new UserInvitationCreateViewModel());
        }

        /// <summary>
        /// Cascading role picker: the roles the inviter may grant that apply in the chosen department
        /// (organization-wide roles for every department, a department role only for its own department).
        /// Same scope, active and escalation filters as the POST.
        /// </summary>
        [HttpGet]
        public ActionResult RolesForDepartment(int? departmentId)
        {
            var organizationId = GetCurrentOrganizationIdOrDeny();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            if (departmentId.HasValue && !GetInvitableDepartments().Any(d => d.Id == departmentId.Value))
            {
                Response.StatusCode = 403;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { error = "You can only invite into an active department within your scope." }, JsonRequestBehavior.AllowGet);
            }

            var roles = GetRolesForDepartment(organizationId.Value, departmentId);
            return Json(new
            {
                departmentId = departmentId,
                roles = roles.Select(r => new { id = r.Id, name = r.Name, scope = r.DepartmentId.HasValue ? "department" : "org-wide" }).ToList()
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(UserInvitationCreateViewModel model, string returnUrl = null)
        {
            var organizationId = GetCurrentOrganizationIdOrDeny();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            ConfigureCreateViewBag(returnUrl, organizationId.Value, model.DepartmentId, model.RoleId);

            // Department: an active department of this organization within the inviter's department scope.
            if (model.DepartmentId.HasValue && !GetInvitableDepartments().Any(d => d.Id == model.DepartmentId.Value))
            {
                ModelState.AddModelError("DepartmentId", "You can only invite into an active department within your scope.");
            }

            // Role: an active role of this organization that the inviter may grant (escalation guard) and that
            // applies in the chosen department. A department is required unless the role is Company Admin
            // (the only role that is not limited to a department) or the organization has no departments yet.
            if (!model.RoleId.HasValue)
            {
                ModelState.AddModelError("RoleId", "Select a role for the invitee.");
            }
            else
            {
                var grantable = GetInvitableRoles(organizationId.Value).FirstOrDefault(r => r.Id == model.RoleId.Value);
                if (grantable == null)
                {
                    ModelState.AddModelError("RoleId", "You cannot grant the selected role. Choose a role whose permissions you already have.");
                }
                else if (!model.DepartmentId.HasValue && OrganizationHasDepartments() && !IsCompanyAdminRole(grantable))
                {
                    ModelState.AddModelError("DepartmentId", "Select a department for the invitee. Only Company Admin can be invited without a department.");
                }
                else if (!GetRolesForDepartment(organizationId.Value, model.DepartmentId).Any(r => r.Id == model.RoleId.Value))
                {
                    ModelState.AddModelError("RoleId", "The selected role does not apply in the selected department.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var organization = ResolveOrganization(organizationId.Value);
            if (organization == null)
            {
                return HttpNotFound();
            }

            var request = new UserInvitationCreateRequest
            {
                OrganizationId = organizationId.Value,
                InvitedByUserId = User.GetUserId(),
                Email = model.Email,
                RoleId = model.RoleId,
                DepartmentId = model.DepartmentId,
                OrganizationSlug = organization.Slug,
                OrganizationName = organization.Name
            };

            var result = _invitationService.CreateInvitation(request);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors ?? new string[0])
                {
                    ModelState.AddModelError("", error);
                }

                return View(model);
            }

            TempData["Message"] = "Invitation sent to " + model.Email + ".";
            TempData["InviteLink"] = result.InviteLink;
            return RedirectToAction("Index");
        }

        private void ConfigureCreateViewBag(string returnUrl, int organizationId, int? departmentId, int? roleId)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            var departments = GetInvitableDepartments();
            ViewBag.Departments = departments
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(d.ParentDepartmentName) ? d.Name : d.Name + " (" + d.ParentDepartmentName + ")",
                    Selected = departmentId.HasValue && d.Id == departmentId.Value
                })
                .ToList();
            ViewBag.Roles = GetRolesForDepartment(organizationId, departmentId)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.DepartmentId.HasValue ? r.Name + " (this department only)" : r.Name,
                    Selected = roleId.HasValue && r.Id == roleId.Value
                })
                .ToList();
            ViewBag.DepartmentRequired = OrganizationHasDepartments();
            ViewBag.HasInvitableDepartments = departments.Count > 0;
        }

        /// <summary>A grantable role plus its department (null = organization-wide).</summary>
        private class InvitableRoleOption
        {
            public int Id { get; set; }

            public string Name { get; set; }

            public int? DepartmentId { get; set; }
        }

        /// <summary>
        /// Active departments of this organization that the inviter can see (the existing department scope:
        /// Company Admin sees every department, department-scoped users only their own).
        /// </summary>
        private System.Collections.Generic.IList<AssetManagement.Application.ViewModels.DepartmentVm> GetInvitableDepartments()
        {
            return BuildDepartmentService().GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ParentDepartmentName)
                .ToList();
        }

        private bool OrganizationHasDepartments()
        {
            return GetActiveDepartments().Count > 0;
        }

        private static bool IsCompanyAdminRole(Role role)
        {
            return role != null && role.IsSystemRole
                && string.Equals(role.Name, "Company Admin", System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Grantable roles that apply in the department: organization-wide roles (Roles.DepartmentId NULL) for every
        /// department, a department role only for its own department. With no department chosen, only Company Admin
        /// (or, when the organization has no departments yet, every organization-wide role).
        /// </summary>
        private System.Collections.Generic.IList<InvitableRoleOption> GetRolesForDepartment(int organizationId, int? departmentId)
        {
            var roleDepartments = RoleDepartments.GetRoleDepartmentIds(organizationId);
            var noDepartmentAllowsAll = !departmentId.HasValue && !OrganizationHasDepartments();
            return GetInvitableRoles(organizationId)
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
                .Select(x => new InvitableRoleOption { Id = x.Role.Id, Name = x.Role.Name, DepartmentId = x.DepartmentId })
                .ToList();
        }

        private AssetManagement.Infrastructure.Identity.RoleDepartmentScopeRepository _roleDepartments;

        private AssetManagement.Infrastructure.Identity.RoleDepartmentScopeRepository RoleDepartments
        {
            get
            {
                if (_roleDepartments == null)
                {
                    _roleDepartments = new AssetManagement.Infrastructure.Identity.RoleDepartmentScopeRepository(
                        DependencyResolver.Current.GetService<AssetManagement.Infrastructure.Persistence.ISqlConnectionFactory>());
                }

                return _roleDepartments;
            }
        }

        /// <summary>
        /// Roles an inviter may lock into an invitation: this organization's active roles only (never platform roles,
        /// never hardcoded), and only roles whose every permission the inviter already has (no escalation).
        /// </summary>
        private System.Collections.Generic.IList<Role> GetInvitableRoles(int organizationId)
        {
            var roles = UnitOfWork.Repository<Role>().GetAll()
                .Where(r => r.OrganizationId.HasValue && r.OrganizationId.Value == organizationId && r.IsActive)
                .OrderBy(r => r.Name)
                .ToList();
            if (roles.Count == 0)
            {
                return roles;
            }

            var userId = User.GetUserId();
            var auth = BuildAuthorizationService();
            var roleIds = roles.Select(r => r.Id).ToList();
            var rolePermissions = UnitOfWork.Repository<RolePermission>().GetAll()
                .Where(rp => roleIds.Contains(rp.RoleId))
                .ToList();
            var codes = UnitOfWork.Repository<Permission>().GetAll()
                .ToDictionary(p => p.Id, p => p.Code);
            // The inviter's own role permissions (by id) plus anything the authorization service grants them.
            var inviter = string.IsNullOrWhiteSpace(userId) ? null : BuildUserService().GetById(userId);
            var ownPermissionIds = new System.Collections.Generic.HashSet<int>();
            if (inviter != null && inviter.RoleId.HasValue)
            {
                var ownRoleId = inviter.RoleId.Value;
                foreach (var rp in UnitOfWork.Repository<RolePermission>().GetAll().Where(x => x.RoleId == ownRoleId))
                {
                    ownPermissionIds.Add(rp.PermissionId);
                }
            }

            var held = new System.Collections.Generic.Dictionary<int, bool>();
            System.Func<int, bool> has = permissionId =>
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

        private int? GetCurrentOrganizationIdOrDeny()
        {
            return _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
        }

        private Organization ResolveOrganization(int organizationId)
        {
            return UnitOfWork.Repository<Organization>().GetAll()
                .FirstOrDefault(x => x.Id == organizationId);
        }
    }
}
