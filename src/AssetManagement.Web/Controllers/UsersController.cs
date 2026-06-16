using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Users.View")]
    public class UsersController : BaseController
    {
        private readonly IUserService _userService;
        private readonly IRoleService _roleService;
        private readonly IUserAccountService _userAccountService;
        private readonly IOrganizationScopeService _organizationScope;

        public UsersController()
        {
            _userService = BuildUserService();
            _roleService = BuildRoleService();
            _userAccountService = DependencyResolver.Current.GetService<IUserAccountService>();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
        }

        public ActionResult Index(string search = null, int? roleId = null, int? departmentId = null, bool? isActive = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var roles = _roleService.GetRoles().ToList();
            var departments = BuildDepartmentService().GetAll().ToList();
            ViewBag.DepartmentOptions = departments;
            var items = _userService.GetAll();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => ((x.FirstName ?? string.Empty) + " " + (x.LastName ?? string.Empty)).Trim().ToLowerInvariant().Contains(term)
                    || (x.Email ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.EmployeeNumber ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            if (roleId.HasValue)
            {
                items = items.Where(x => x.RoleId == roleId);
            }

            if (departmentId.HasValue)
            {
                items = items.Where(x => x.DepartmentId == departmentId);
            }

            if (isActive.HasValue)
            {
                items = items.Where(x => x.IsActive == isActive.Value);
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "email":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.Email) : items.OrderBy(x => x.Email);
                    break;
                case "role":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.RoleName) : items.OrderBy(x => x.RoleName);
                    break;
                case "status":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.IsActive) : items.OrderBy(x => x.IsActive);
                    break;
                default:
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => (x.FirstName ?? string.Empty) + " " + (x.LastName ?? string.Empty))
                        : items.OrderBy(x => (x.FirstName ?? string.Empty) + " " + (x.LastName ?? string.Empty));
                    sort = "name";
                    break;
            }

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
            return View(BuildListPage(items, search, sort, direction, page, pageSize));
        }

        public ActionResult Details(string id, string returnUrl = null)
        {
            var model = _userService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            var role = model.RoleId.HasValue ? _roleService.GetById(model.RoleId.Value) : null;
            ViewBag.RoleName = role?.Name ?? model.RoleName;
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.AssignedAssetCount = BuildAssetService().CountAssets(new AssetFilterVm { CustodianUserId = model.Id });
            ViewBag.Roles = _roleService.GetRoles();
            ViewBag.Departments = BuildDepartmentService().GetAll();
            return View(model);
        }

        [PermissionAuthorize("Users.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.Roles = _roleService.GetRoles();
            ViewBag.Departments = BuildDepartmentService().GetAll();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(new UserCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Create")]
        public ActionResult Create(UserCreateViewModel model, string returnUrl = null)
        {
            ViewBag.Roles = _roleService.GetRoles();
            ViewBag.Departments = BuildDepartmentService().GetAll();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!IsAdministratorRole(model.RoleId) && !model.DepartmentId.HasValue)
            {
                ModelState.AddModelError("DepartmentId", "Department is required for non-administrator users.");
                return View(model);
            }

            var user = new UserAccountCreateRequest
            {
                Email = model.Email,
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                DepartmentId = model.DepartmentId,
                PositionTitle = model.PositionTitle,
                RoleId = model.RoleId,
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
            TempData["Guidance"] = "Next step: review the user profile and confirm their role and department before assigning assets.";
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

            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Users.Edit")]
        public ActionResult AssignDepartment(string userId, int? departmentId, string returnUrl = null)
        {
            if (departmentId.HasValue && BuildDepartmentService().GetById(departmentId.Value) == null)
            {
                TempData["Error"] = "That department no longer exists.";
                return RedirectToReturnUrl(returnUrl, "Details", null, new { id = userId });
            }

            _userService.AssignDepartment(userId, departmentId);
            TempData["Message"] = "User department updated.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = userId });
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
                TempData["Message"] = "Failed login attempts cleared for this user.";
            }

            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = userId });
        }

        private bool IsAdministratorRole(int roleId)
        {
            var role = _roleService.GetById(roleId);
            return role != null
                && role.IsSystemRole
                && string.Equals(role.Name, "Company Admin", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
