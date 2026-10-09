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
            return GrantableRoleResolver.IsCompanyAdminRole(role);
        }

        /// <summary>Cascade filter (moved unchanged to GrantableRoleResolver in rel1009g; shared with Users/Create).</summary>
        private System.Collections.Generic.IList<GrantableRoleOption> GetRolesForDepartment(int organizationId, int? departmentId)
        {
            return RoleResolver.GetRolesForDepartment(organizationId, departmentId);
        }

        /// <summary>Active org roles the inviter may grant, no escalation (GrantableRoleResolver).</summary>
        private System.Collections.Generic.IList<Role> GetInvitableRoles(int organizationId)
        {
            return RoleResolver.GetGrantableRoles(organizationId);
        }

        private GrantableRoleResolver _roleResolver;

        private GrantableRoleResolver RoleResolver
        {
            get
            {
                if (_roleResolver == null)
                {
                    _roleResolver = new GrantableRoleResolver(
                        UnitOfWork,
                        BuildAuthorizationService(),
                        BuildUserService(),
                        new AssetManagement.Infrastructure.Identity.RoleDepartmentScopeRepository(
                            DependencyResolver.Current.GetService<AssetManagement.Infrastructure.Persistence.ISqlConnectionFactory>()),
                        User.GetUserId(),
                        OrganizationHasDepartments);
                }

                return _roleResolver;
            }
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
