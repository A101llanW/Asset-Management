using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.ViewModels.Platform;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Areas.Platform.Controllers
{
    [PermissionAuthorize("Platform.Users.View")]
    public class UsersController : Controller
    {
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IUserAccountQueryRepository _userAccountQuery;
        private readonly IUserAccountService _userAccountService;
        private readonly IUserService _userService;
        private readonly IReferenceDataCache _referenceDataCache;
        private readonly IUnitOfWork _unitOfWork;

        public UsersController()
        {
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _userAccountQuery = DependencyResolver.Current.GetService<IUserAccountQueryRepository>();
            _userAccountService = DependencyResolver.Current.GetService<IUserAccountService>();
            _userService = DependencyResolver.Current.GetService<IUserService>();
            _referenceDataCache = DependencyResolver.Current.GetService<IReferenceDataCache>();
            _unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
        }

        public ActionResult Index(
            string search = null,
            int? organizationId = null,
            string userScope = null,
            int? roleId = null,
            bool? isActive = null,
            string sort = "name",
            string direction = "asc",
            int page = 1,
            int pageSize = 25)
        {
            EnsurePlatformAccess();

            var organizations = LoadOrganizations();
            var items = _userAccountQuery.GetAllUsersForPlatformAdmin().AsEnumerable();

            if (string.Equals(userScope, "system", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(x => x.IsSystemUser);
            }
            else if (string.Equals(userScope, "company", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(x => !x.IsSystemUser);
            }

            if (organizationId.HasValue)
            {
                items = items.Where(x => x.OrganizationId == organizationId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x =>
                    ((x.FirstName ?? string.Empty) + " " + (x.LastName ?? string.Empty)).Trim().ToLowerInvariant().Contains(term)
                    || (x.Email ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.EmployeeNumber ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.OrganizationName ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            if (roleId.HasValue)
            {
                items = items.Where(x => x.RoleId == roleId);
            }

            if (isActive.HasValue)
            {
                items = items.Where(x => x.IsActive == isActive.Value);
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "email":
                    items = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.Email)
                        : items.OrderBy(x => x.Email);
                    break;
                case "organization":
                    items = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.OrganizationName ?? string.Empty)
                        : items.OrderBy(x => x.OrganizationName ?? string.Empty);
                    break;
                case "role":
                    items = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.RoleName)
                        : items.OrderBy(x => x.RoleName);
                    break;
                case "status":
                    items = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => x.IsActive)
                        : items.OrderBy(x => x.IsActive);
                    break;
                default:
                    items = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase)
                        ? items.OrderByDescending(x => (x.FirstName ?? string.Empty) + " " + (x.LastName ?? string.Empty))
                        : items.OrderBy(x => (x.FirstName ?? string.Empty) + " " + (x.LastName ?? string.Empty));
                    sort = "name";
                    break;
            }

            var roles = BuildRoleFilterOptions(items);
            ViewBag.Organizations = new SelectList(organizations, "Id", "Name", organizationId);
            ViewBag.RoleFilter = new SelectList(roles, "Id", "Name", roleId);
            ViewBag.UserScope = userScope;
            ViewBag.StatusFilter = new SelectList(new[]
            {
                new { Value = "", Text = "All statuses" },
                new { Value = "true", Text = "Active" },
                new { Value = "false", Text = "Inactive" }
            }, "Value", "Text", isActive.HasValue ? isActive.Value.ToString().ToLowerInvariant() : string.Empty);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            ViewBag.CanManage = HtmlHasPermission("Platform.Users.Manage");

            var category = ResolveCategory(userScope, Request.QueryString["category"]);
            var viewModel = BuildIndexViewModel(items, search, organizationId, userScope, roleId, isActive, sort, direction, category, page, pageSize);
            ViewBag.Category = viewModel.Category;

            return View(viewModel);
        }

        public ActionResult Details(string id)
        {
            EnsurePlatformAccess();
            var model = _userAccountQuery.GetUserByIdForPlatform(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.CanManage = HtmlHasPermission("Platform.Users.Manage");
            ViewBag.Roles = LoadRolesForUser(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Platform.Users.Manage")]
        public ActionResult AssignRole(string userId, int roleId)
        {
            EnsurePlatformAccess();

            var user = _userAccountQuery.GetUserByIdForPlatform(userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            string roleError;
            if (!ValidateRoleForUser(roleId, user, out roleError))
            {
                TempData["Error"] = roleError;
                return RedirectToAction("Details", new { id = userId });
            }

            try
            {
                _userService.AssignRole(userId, roleId);
                TempData["Message"] = "User role updated.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = userId });
        }

        [PermissionAuthorize("Platform.Users.Manage")]
        public ActionResult Create(int? organizationId = null)
        {
            EnsurePlatformAccess();
            PopulateCreateViewBags(organizationId);
            return View(new PlatformUserCreateViewModel { OrganizationId = organizationId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Platform.Users.Manage")]
        public ActionResult Create(PlatformUserCreateViewModel model)
        {
            EnsurePlatformAccess();
            PopulateCreateViewBags(model.OrganizationId);

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string roleError;
            if (!ValidateRoleForTargetOrganization(model.RoleId, model.OrganizationId, out roleError))
            {
                ModelState.AddModelError("RoleId", roleError);
                return View(model);
            }

            var result = _userAccountService.CreateUser(new UserAccountCreateRequest
            {
                Email = model.Email,
                EmployeeNumber = model.EmployeeNumber,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                RoleId = model.RoleId,
                OrganizationId = model.OrganizationId
            }, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors ?? new string[0])
                {
                    ModelState.AddModelError(string.Empty, error);
                }

                return View(model);
            }

            TempData["Message"] = "User created successfully.";
            return RedirectToAction("Details", new { id = result.UserId });
        }

        private void PopulateCreateViewBags(int? organizationId)
        {
            var organizations = LoadOrganizations();
            ViewBag.Organizations = new SelectList(organizations, "Id", "Name", organizationId);
            ViewBag.IsSystemUserContext = !organizationId.HasValue;

            if (organizationId.HasValue)
            {
                ViewBag.Roles = _referenceDataCache.GetRoles(organizationId.Value);
            }
            else
            {
                ViewBag.Roles = _userAccountQuery.GetPlatformRoles();
            }
        }

        private IList<RoleVm> LoadRolesForUser(PlatformUserListItemVm user)
        {
            if (user == null)
            {
                return new List<RoleVm>();
            }

            if (user.IsSystemUser)
            {
                return _userAccountQuery.GetPlatformRoles();
            }

            if (!user.OrganizationId.HasValue)
            {
                return new List<RoleVm>();
            }

            return _referenceDataCache.GetRoles(user.OrganizationId.Value);
        }

        private bool ValidateRoleForUser(int roleId, PlatformUserListItemVm user, out string error)
        {
            error = null;
            if (user == null)
            {
                error = "User not found.";
                return false;
            }

            if (user.IsSystemUser)
            {
                var platformRole = _userAccountQuery.GetPlatformRoles().FirstOrDefault(x => x.Id == roleId);
                if (platformRole == null)
                {
                    error = "Selected role is not valid for system users.";
                    return false;
                }

                return true;
            }

            if (!user.OrganizationId.HasValue)
            {
                error = "User organization is missing.";
                return false;
            }

            var role = _referenceDataCache.GetRoles(user.OrganizationId.Value).FirstOrDefault(x => x.Id == roleId);
            if (role == null)
            {
                error = "Selected role is not valid for this user's organization.";
                return false;
            }

            return true;
        }

        private IList<Organization> LoadOrganizations()
        {
            return _unitOfWork.Repository<Organization>().Query()
                .Where(o => o.IsActive)
                .OrderBy(o => o.Name)
                .ToList();
        }

        private static IList<RoleVm> BuildRoleFilterOptions(IEnumerable<PlatformUserListItemVm> items)
        {
            return items
                .Where(x => x.RoleId.HasValue)
                .GroupBy(x => x.RoleId.Value)
                .Select(g => new RoleVm
                {
                    Id = g.Key,
                    Name = g.First().RoleName ?? ("Role " + g.Key)
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        private bool ValidateRoleForTargetOrganization(int roleId, int? organizationId, out string error)
        {
            error = null;
            if (organizationId.HasValue)
            {
                var role = _referenceDataCache.GetRoles(organizationId.Value).FirstOrDefault(x => x.Id == roleId);
                if (role == null)
                {
                    error = "Selected role is not valid for the chosen organization.";
                    return false;
                }

                return true;
            }

            var platformRole = _userAccountQuery.GetPlatformRoles().FirstOrDefault(x => x.Id == roleId);
            if (platformRole == null)
            {
                error = "Selected role is not valid for system users.";
                return false;
            }

            return true;
        }

        private void EnsurePlatformAccess()
        {
            if (_organizationScope.IsImpersonating())
            {
                throw new UnauthorizedAccessException("Platform user management is not available during impersonation.");
            }

            if (!_organizationScope.IsActualPlatformAdmin())
            {
                throw new UnauthorizedAccessException("Platform access required.");
            }
        }

        private bool HtmlHasPermission(string permissionCode)
        {
            var userId = FormsAuthHelper.GetUserId(User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var auth = DependencyResolver.Current.GetService<IAuthorizationService>();
            return auth != null && auth.HasPermission(userId, permissionCode);
        }

        private static string ResolveCategory(string userScope, string category)
        {
            if (string.Equals(userScope, "system", StringComparison.OrdinalIgnoreCase))
            {
                return "system";
            }

            if (string.Equals(userScope, "company", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrWhiteSpace(category)
                    || string.Equals(category, "system", StringComparison.OrdinalIgnoreCase)
                    ? "tenant"
                    : category.Trim().ToLowerInvariant();
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return "system";
            }

            var normalized = category.Trim().ToLowerInvariant();
            if (normalized == "system" || normalized == "admins" || normalized == "tenant")
            {
                return normalized;
            }

            return "system";
        }

        private static PlatformUserIndexViewModel BuildIndexViewModel(
            IEnumerable<PlatformUserListItemVm> filteredItems,
            string search,
            int? organizationId,
            string userScope,
            int? roleId,
            bool? isActive,
            string sort,
            string direction,
            string category,
            int page,
            int pageSize)
        {
            var materialized = filteredItems.ToList();
            var buckets = BuildUserBuckets(materialized);
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);

            var viewModel = new PlatformUserIndexViewModel
            {
                Search = search,
                OrganizationId = organizationId,
                UserScope = userScope,
                RoleId = roleId,
                IsActive = isActive,
                Sort = sort,
                Direction = direction,
                Category = category,
                Page = page,
                PageSize = safePageSize,
                SystemUserCount = buckets.SystemUsers.Count,
                OrganizationAdminCount = buckets.OrganizationAdmins.Count,
                OrganizationCount = buckets.OrganizationGroups.Count,
                TotalCount = materialized.Count
            };

            switch (category)
            {
                case "admins":
                    PaginateList(
                        buckets.OrganizationAdmins,
                        page,
                        safePageSize,
                        viewModel,
                        items => viewModel.OrganizationAdmins = items);
                    break;
                case "tenant":
                    PaginateOrganizationGroups(buckets.OrganizationGroups, page, safePageSize, viewModel);
                    break;
                default:
                    PaginateList(
                        buckets.SystemUsers,
                        page,
                        safePageSize,
                        viewModel,
                        items => viewModel.SystemUsers = items);
                    viewModel.Category = "system";
                    break;
            }

            return viewModel;
        }

        private static UserBuckets BuildUserBuckets(IList<PlatformUserListItemVm> users)
        {
            var buckets = new UserBuckets();
            foreach (var user in users)
            {
                AddUserToBuckets(buckets, user);
            }

            buckets.OrganizationGroups = buckets.UsersByOrganization
                .OrderBy(x => x.Key)
                .Select(x => new PlatformUserOrganizationGroupVm
                {
                    OrganizationName = x.Key,
                    OrganizationId = x.Value.First().OrganizationId ?? 0,
                    Users = x.Value
                })
                .ToList();

            return buckets;
        }

        private static void AddUserToBuckets(UserBuckets buckets, PlatformUserListItemVm user)
        {
            if (user.IsSystemUser)
            {
                buckets.SystemUsers.Add(user);
            }

            if (user.IsOrganizationAdmin)
            {
                buckets.OrganizationAdmins.Add(user);
            }

            if (user.IsSystemUser || user.IsOrganizationAdmin)
            {
                return;
            }

            if (!user.OrganizationId.HasValue)
            {
                return;
            }

            var organizationName = string.IsNullOrWhiteSpace(user.OrganizationName)
                ? "Unknown"
                : user.OrganizationName;
            List<PlatformUserListItemVm> organizationUsers;
            if (!buckets.UsersByOrganization.TryGetValue(organizationName, out organizationUsers))
            {
                organizationUsers = new List<PlatformUserListItemVm>();
                buckets.UsersByOrganization[organizationName] = organizationUsers;
            }

            organizationUsers.Add(user);
        }

        private static void PaginateList(
            IList<PlatformUserListItemVm> source,
            int page,
            int pageSize,
            PlatformUserIndexViewModel viewModel,
            Action<IList<PlatformUserListItemVm>> assignItems)
        {
            var totalCount = source.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);
            var items = source.Skip((safePage - 1) * pageSize).Take(pageSize).ToList();

            assignItems(items);
            viewModel.Page = safePage;
            viewModel.ActiveTotalCount = totalCount;
            viewModel.ActiveTotalPages = totalPages;
            viewModel.ActiveStartItem = totalCount == 0 ? 0 : ((safePage - 1) * pageSize) + 1;
            viewModel.ActiveEndItem = Math.Min(safePage * pageSize, totalCount);
        }

        private static void PaginateOrganizationGroups(
            IList<PlatformUserOrganizationGroupVm> source,
            int page,
            int pageSize,
            PlatformUserIndexViewModel viewModel)
        {
            var totalCount = source.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);
            var groups = source.Skip((safePage - 1) * pageSize).Take(pageSize).ToList();

            viewModel.OrganizationGroups = groups;
            viewModel.Page = safePage;
            viewModel.ActiveTotalCount = totalCount;
            viewModel.ActiveTotalPages = totalPages;
            viewModel.ActiveStartItem = totalCount == 0 ? 0 : ((safePage - 1) * pageSize) + 1;
            viewModel.ActiveEndItem = Math.Min(safePage * pageSize, totalCount);
        }

        private sealed class UserBuckets
        {
            public IList<PlatformUserListItemVm> SystemUsers { get; } = new List<PlatformUserListItemVm>();

            public IList<PlatformUserListItemVm> OrganizationAdmins { get; } = new List<PlatformUserListItemVm>();

            public Dictionary<string, List<PlatformUserListItemVm>> UsersByOrganization { get; } =
                new Dictionary<string, List<PlatformUserListItemVm>>(StringComparer.OrdinalIgnoreCase);

            public IList<PlatformUserOrganizationGroupVm> OrganizationGroups { get; set; } =
                new List<PlatformUserOrganizationGroupVm>();
        }

        private static ListPageViewModel<T> BuildListPage<T>(IEnumerable<T> source, string search, string sort, string direction, int page, int pageSize)
        {
            var safePageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var materialized = source.ToList();
            var totalCount = materialized.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / safePageSize));
            var safePage = Math.Min(Math.Max(page, 1), totalPages);

            return new ListPageViewModel<T>
            {
                Items = materialized.Skip((safePage - 1) * safePageSize).Take(safePageSize).ToList(),
                Search = search,
                Sort = sort,
                Direction = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase) ? "desc" : "asc",
                Page = safePage,
                PageSize = safePageSize,
                TotalCount = totalCount
            };
        }
    }
}
