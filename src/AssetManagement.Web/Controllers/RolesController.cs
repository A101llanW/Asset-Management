using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Roles.View")]
    public class RolesController : BaseController
    {
        private readonly IRoleService _roleService;
        private readonly IPermissionService _permissionService;

        public RolesController()
        {
            _roleService = BuildRoleService();
            _permissionService = BuildPermissionService();
        }

        public ActionResult Index(string search = null, bool? isActive = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var items = _roleService.GetRoles();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Description ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            if (isActive.HasValue)
            {
                items = items.Where(x => x.IsActive == isActive.Value);
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "status":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.IsActive) : items.OrderBy(x => x.IsActive);
                    break;
                case "system":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.IsSystemRole) : items.OrderBy(x => x.IsSystemRole);
                    break;
                default:
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.Name) : items.OrderBy(x => x.Name);
                    sort = "name";
                    break;
            }

            ViewBag.StatusFilter = new SelectList(new[]
            {
                new { Value = "", Text = "All statuses" },
                new { Value = "true", Text = "Active" },
                new { Value = "false", Text = "Inactive" }
            }, "Value", "Text", isActive.HasValue ? isActive.Value.ToString().ToLowerInvariant() : string.Empty);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(BuildListPage(items, search, sort, direction, page, pageSize));
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var role = _roleService.GetById(id);
            if (role == null)
            {
                return HttpNotFound();
            }

            var assignedPermissionIds = UnitOfWork.Repository<AssetManagement.Domain.Entities.RolePermission>()
                .Find(x => x.RoleId == id)
                .Select(x => x.PermissionId)
                .ToList();
            var permissions = _permissionService.GetAll()
                .Where(x => assignedPermissionIds.Contains(x.Id))
                .OrderBy(x => x.Module)
                .ThenBy(x => x.Name)
                .ToList();

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PermissionGroups = permissions.GroupBy(x => x.Module).ToList();
            ViewBag.UserCount = BuildUserService().GetAll().Count(x => x.RoleId == id);
            return View(role);
        }

        [PermissionAuthorize("Roles.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(new RoleCreateEditVm
            {
                PermissionGroups = _permissionService.GetGroupedPermissions()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Roles.Create")]
        public ActionResult Create(RoleCreateEditVm model, string returnUrl = null)
        {
            model.SelectedPermissionIds = model.SelectedPermissionIds ?? new System.Collections.Generic.List<int>();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            if (!ModelState.IsValid)
            {
                model.PermissionGroups = _permissionService.GetGroupedPermissions();
                return View(model);
            }

            var roleId = _roleService.Create(model);
            TempData["Message"] = "Role created successfully.";
            TempData["Guidance"] = "Next step: review the role details and confirm the assigned permissions before assigning it to users.";
            return RedirectToAction("Details", new { id = roleId, returnUrl = ViewBag.ReturnUrl });
        }

        [PermissionAuthorize("Roles.Edit")]
        public ActionResult Edit(int id, string returnUrl = null)
        {
            var role = _roleService.GetById(id);
            if (role == null)
            {
                return HttpNotFound();
            }

            var selected = UnitOfWork.Repository<AssetManagement.Domain.Entities.RolePermission>().Find(x => x.RoleId == id).Select(x => x.PermissionId).ToList();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            return View(new RoleCreateEditVm
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole,
                SelectedPermissionIds = selected,
                PermissionGroups = _permissionService.GetGroupedPermissions()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Roles.Edit")]
        public ActionResult Edit(RoleCreateEditVm model, string returnUrl = null)
        {
            model.SelectedPermissionIds = model.SelectedPermissionIds ?? new System.Collections.Generic.List<int>();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            if (!ModelState.IsValid)
            {
                model.PermissionGroups = _permissionService.GetGroupedPermissions();
                return View(model);
            }

            _roleService.Update(model);
            TempData["Message"] = "Role updated successfully.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }
    }
}
