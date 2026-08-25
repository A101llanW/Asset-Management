using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.Security;
using AssetManagement.Web.Filters;
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
            var isDepartmentScoped = IsDepartmentScopedUserAccess();
            var scopedDepartmentId = isDepartmentScoped ? GetScopedDepartmentIdOrDeny() : null;
            if (isDepartmentScoped && !scopedDepartmentId.HasValue)
            {
                return new HttpStatusCodeResult(403, "Your account is not assigned to a department.");
            }

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
            return View(model);
        }

        [PermissionAuthorize("Users.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(new UserCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Create")]
        public ActionResult Create(UserCreateViewModel model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new UserAccountCreateRequest
            {
                Email = model.Email,
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                PositionTitle = model.PositionTitle,
                OrganizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId()
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

            TempData["Message"] = "User created successfully.";
            TempData["Guidance"] = "Open the user profile to assign their department and role before assigning assets.";
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

        private bool IsDepartmentScopedUserAccess()
        {
            return !HasUsersViewPermission() && HasUsersViewDepartmentPermission();
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

            var departmentId = GetScopedDepartmentIdOrDeny();
            return departmentId.HasValue && user.DepartmentId == departmentId.Value;
        }
    }
}
