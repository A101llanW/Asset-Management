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

            ConfigureCreateViewBag(returnUrl, organizationId.Value);
            return View(new UserInvitationCreateViewModel());
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

            ConfigureCreateViewBag(returnUrl, organizationId.Value);

            // Server-side escalation guard: only an active role of this organization that the inviter may grant.
            if (!model.RoleId.HasValue)
            {
                ModelState.AddModelError("RoleId", "Select a role for the invitee.");
            }
            else if (!GetInvitableRoles(organizationId.Value).Any(r => r.Id == model.RoleId.Value))
            {
                ModelState.AddModelError("RoleId", "You cannot grant the selected role. Choose a role whose permissions you already have.");
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

        private void ConfigureCreateViewBag(string returnUrl, int organizationId)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.Roles = new SelectList(GetInvitableRoles(organizationId), "Id", "Name");
            ViewBag.Departments = new SelectList(BuildDepartmentService().GetAll().Where(x => x.IsActive), "Id", "Name");
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
