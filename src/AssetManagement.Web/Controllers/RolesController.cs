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

        public ActionResult Index()
        {
            return View(_roleService.GetRoles());
        }

        [PermissionAuthorize("Roles.Create")]
        public ActionResult Create()
        {
            return View(new RoleCreateEditVm
            {
                PermissionGroups = _permissionService.GetGroupedPermissions()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Roles.Create")]
        public ActionResult Create(RoleCreateEditVm model)
        {
            model.SelectedPermissionIds = model.SelectedPermissionIds ?? new System.Collections.Generic.List<int>();
            if (!ModelState.IsValid)
            {
                model.PermissionGroups = _permissionService.GetGroupedPermissions();
                return View(model);
            }

            _roleService.Create(model);
            TempData["Message"] = "Role created successfully.";
            return RedirectToAction("Index");
        }

        [PermissionAuthorize("Roles.Edit")]
        public ActionResult Edit(int id)
        {
            var role = _roleService.GetById(id);
            if (role == null)
            {
                return HttpNotFound();
            }

            var selected = UnitOfWork.Repository<AssetManagement.Domain.Entities.RolePermission>().Find(x => x.RoleId == id).Select(x => x.PermissionId).ToList();
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
        public ActionResult Edit(RoleCreateEditVm model)
        {
            model.SelectedPermissionIds = model.SelectedPermissionIds ?? new System.Collections.Generic.List<int>();
            if (!ModelState.IsValid)
            {
                model.PermissionGroups = _permissionService.GetGroupedPermissions();
                return View(model);
            }

            _roleService.Update(model);
            TempData["Message"] = "Role updated successfully.";
            return RedirectToAction("Index");
        }
    }
}
