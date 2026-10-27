using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.Security;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.ViewModels;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [AnyPermissionAuthorize("Users.View", "Users.ViewDepartment")]
    [ModuleAccess(ModulePermissionCatalog.Users)]
    public class UsersController : BaseController
    {
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;
        private readonly IUserAccountService _userAccountService;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IDepartmentScopeService _departmentScope;

        public UsersController()
        {
            _userService = BuildUserService();
            _roleService = BuildRoleService();
            _userAccountService = DependencyResolver.Current.GetService<IUserAccountService>();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _departmentScope = DependencyResolver.Current.GetService<IDepartmentScopeService>();
        }

        public ActionResult Index(string search = null, int? roleId = null, int? departmentId = null, bool? isActive = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            // A Users.ViewDepartment holder with no department is organization-wide (DepartmentScopeRules): they
            // list every user of the current organization instead of getting a 403.
            var isDepartmentScoped = IsDepartmentScopedUserAccess();
            var scopedDepartmentId = isDepartmentScoped ? GetScopedDepartmentIdOrDeny() : null;

            var roles = _roleService.GetRoles().ToList();
            var departments = BuildDepartmentService().GetAll().ToList();
            if (isDepartmentScoped)
            {
                departments = departments.Where(x => x.Id == scopedDepartmentId.Value).ToList();
                departmentId = scopedDepartmentId;
            }

            ViewBag.IsDepartmentScoped = isDepartmentScoped;
            ViewBag.ScopedDepartmentName = isDepartmentScoped && departments.Count > 0
                ? departments[0].Name
                : null;

            var filter = new UserListFilterVm
            {
                Search = search,
                RoleId = roleId,
                DepartmentId = departmentId,
                IsActive = isActive
            };
            var pageResult = _userService.GetListPage(filter, sort, direction, page, pageSize);

            ViewBag.Roles = roles;
            ViewBag.Departments = new SelectList(departments, "Id", "Name", departmentId);
            ViewBag.RoleFilter = new SelectList(roles, "Id", "Name", roleId);
            ViewBag.StatusFilter = new SelectList(new[]
            {
                new { Value = "", Text = "All statuses" },
                new { Value = "true", Text = "Active" },
                new { Value = "false", Text = "Inactive" }
            }, "Value", "Text", isActive.HasValue ? isActive.Value.ToString().ToLowerInvariant() : string.Empty);
            SetListSortViewBag(sort, direction);
            return View(ToListPage(pageResult));
        }

        public ActionResult Details(string id, string returnUrl = null)
        {
            var model = _userService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            if (!CanAccessUser(model))
            {
                return new HttpStatusCodeResult(403);
            }

            var role = model.RoleId.HasValue ? _roleService.GetById(model.RoleId.Value) : null;
            ViewBag.RoleName = role?.Name ?? model.RoleName;
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.AssignedAssetCount = BuildAssetService().CountAssets(new AssetFilterVm { CustodianUserId = model.Id });
            ViewBag.Roles = _roleService.GetRoles();
            ViewBag.Departments = BuildDepartmentService().GetAll();
            ViewBag.IsDepartmentScoped = IsDepartmentScopedUserAccess();
            ViewBag.IsSelf = string.Equals(model.Id, User.GetUserId(), System.StringComparison.OrdinalIgnoreCase);
            return View(model);
        }

        [PermissionAuthorize("Users.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            ConfigureCreateLookups(organizationId.Value, null, null);
            return View(new UserCreateViewModel());
        }

        /// <summary>Cascading Department -> Role picker for Create user (same filters as Invite user).</summary>
        [HttpGet]
        [PermissionAuthorize("Users.Create")]
        public ActionResult RolesForDepartment(int? departmentId)
        {
            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            if (departmentId.HasValue && !GetAssignableDepartments().Any(d => d.Id == departmentId.Value))
            {
                Response.StatusCode = 403;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { error = "You can only choose an active department within your scope." }, JsonRequestBehavior.AllowGet);
            }

            var roles = RoleResolver.GetRolesForDepartment(organizationId.Value, departmentId);
            return Json(new
            {
                departmentId = departmentId,
                roles = roles.Select(r => new { id = r.Id, name = r.Name, scope = r.DepartmentId.HasValue ? "department" : "org-wide" }).ToList()
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Create")]
        public ActionResult Create(UserCreateViewModel model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new HttpStatusCodeResult(403);
            }

            model = model ?? new UserCreateViewModel();
            ConfigureCreateLookups(organizationId.Value, model.DepartmentId, model.RoleId);

            // rel1009g: role is chosen from the organization's active roles (no free typing), with the same scope,
            // active, escalation and Department -> Role checks as Invite user. Position Title is derived from it.
            var roleError = RoleResolver.ValidateRoleChoice(
                organizationId.Value,
                model.RoleId,
                model.DepartmentId,
                GetAssignableDepartments(),
                "Select a role for the user.");
            if (roleError.HasValue)
            {
                ModelState.AddModelError(roleError.Value.Key, roleError.Value.Value);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var selectedRole = RoleResolver.GetGrantableRoles(organizationId.Value).First(r => r.Id == model.RoleId.Value);
            var user = new UserAccountCreateRequest
            {
                Email = model.Email,
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                DepartmentId = model.DepartmentId,
                RoleId = selectedRole.Id,
                PositionTitle = selectedRole.Name,
                OrganizationId = organizationId
            };

            var result = _userAccountService.CreateUser(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
                }

                return View(model);
            }

            AuditUserAction("Users.Create", result.UserId, null, "Role=" + selectedRole.Name + ";DepartmentId=" + (model.DepartmentId.HasValue ? model.DepartmentId.Value.ToString() : ""));
            TempData["Message"] = "User created successfully.";
            return RedirectToAction("Details", new { id = result.UserId, returnUrl = ViewBag.ReturnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Edit")]
        public ActionResult AssignRole(string userId, int roleId, string returnUrl = null)
        {
            try
            {
                _userService.AssignRole(userId, roleId);
                TempData["Message"] = "User role updated.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = userId, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Edit")]
        public ActionResult AssignDepartment(string userId, int? departmentId, string returnUrl = null)
        {
            try
            {
                if (departmentId.HasValue && BuildDepartmentService().GetById(departmentId.Value) == null)
                {
                    TempData["Error"] = "That department no longer exists.";
                }
                else
                {
                    _userService.AssignDepartment(userId, departmentId);
                    TempData["Message"] = "User department updated.";
                }
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = userId, returnUrl });
        }

        /// <summary>Deactivate / reactivate (rel1009g). Inactive users cannot sign in; existing sessions end (token rotated).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Edit")]
        public ActionResult SetActive(string userId, bool isActive, string returnUrl = null)
        {
            var target = _userService.GetById(userId);
            if (target == null)
            {
                return HttpNotFound();
            }

            if (!CanAccessUser(target))
            {
                return new HttpStatusCodeResult(403);
            }

            try
            {
                _userService.SetActive(userId, isActive);
                TempData["Message"] = isActive ? "User reactivated. They can sign in again." : "User deactivated. They can no longer sign in.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = userId });
        }

        /// <summary>Hard delete (rel1009g) only when the user has no linked history; otherwise blocked with guidance.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Delete")]
        public ActionResult Delete(string userId, string returnUrl = null)
        {
            var target = _userService.GetById(userId);
            if (target == null)
            {
                return HttpNotFound();
            }

            if (!CanAccessUser(target))
            {
                return new HttpStatusCodeResult(403);
            }

            try
            {
                _userService.Delete(userId);
                TempData["Message"] = "User " + target.Email + " was deleted.";
                return RedirectToAction("Index");
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction("Details", new { id = userId, returnUrl });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Edit")]
        public ActionResult UnlockAccount(string userId, string returnUrl = null)
        {
            var accountSecurity = DependencyResolver.Current.GetService<IAccountSecurityService>();
            if (accountSecurity == null)
            {
                TempData["Error"] = "Account security service is unavailable.";
            }
            else
            {
                accountSecurity.ClearFailedLoginAttemptsForUser(userId);
                if (accountSecurity != null)
                {
                    accountSecurity.RotateUserAccessToken(userId);
                }

                TempData["Message"] = "Failed login attempts cleared for this user.";
            }

            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = userId });
        }

        private void AuditUserAction(string action, string entityId, string oldValues, string newValues)
        {
            var audit = DependencyResolver.Current.GetService<IAuditWriter>();
            if (audit != null)
            {
                audit.Write(action, "ApplicationUser", entityId, oldValues, newValues);
            }
        }

        private void ConfigureCreateLookups(int organizationId, int? departmentId, int? roleId)
        {
            var departments = GetAssignableDepartments();
            ViewBag.Departments = departments
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(d.ParentDepartmentName) ? d.Name : d.Name + " (" + d.ParentDepartmentName + ")",
                    Selected = departmentId.HasValue && d.Id == departmentId.Value
                })
                .ToList();
            ViewBag.Roles = RoleResolver.GetRolesForDepartment(organizationId, departmentId)
                .Select(r => new SelectListItem
                {
                    Value = r.Id.ToString(),
                    Text = r.DepartmentId.HasValue ? r.Name + " (this department only)" : r.Name,
                    Selected = roleId.HasValue && r.Id == roleId.Value
                })
                .ToList();
            ViewBag.DepartmentRequired = GetActiveDepartments().Count > 0;
        }

        /// <summary>Active departments the actor can see (same department scope as Invite user).</summary>
        private System.Collections.Generic.IList<DepartmentVm> GetAssignableDepartments()
        {
            return BuildDepartmentService().GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ThenBy(x => x.ParentDepartmentName)
                .ToList();
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
                        () => GetActiveDepartments().Count > 0);
                }

                return _roleResolver;
            }
        }

        private bool IsDepartmentScopedUserAccess()
        {
            return !HasUsersViewPermission()
                && HasUsersViewDepartmentPermission()
                && !IsCurrentUserUnassignedMember();
        }

        private bool HasUsersViewPermission()
        {
            return BuildAuthorizationService().HasPermission(User.GetUserId(), "Users.View");
        }

        private bool HasUsersViewDepartmentPermission()
        {
            return BuildAuthorizationService().HasPermission(User.GetUserId(), "Users.ViewDepartment");
        }

        private int? GetScopedDepartmentIdOrDeny()
        {
            if (_departmentScope != null && _departmentScope.ScopedDepartmentId.HasValue)
            {
                return _departmentScope.ScopedDepartmentId;
            }

            return GetCurrentUserDepartmentId();
        }

        private bool CanAccessUser(UserVm user)
        {
            if (user == null || HasUsersViewPermission())
            {
                return user != null;
            }

            if (!HasUsersViewDepartmentPermission())
            {
                return false;
            }

            // No department: organization-wide. GetById is organization-scoped, so this never crosses tenants.
            if (IsCurrentUserUnassignedMember())
            {
                return true;
            }

            var departmentId = GetScopedDepartmentIdOrDeny();
            return departmentId.HasValue && user.DepartmentId == departmentId.Value;
        }
    }
}
