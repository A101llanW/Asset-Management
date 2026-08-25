using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Roles.View")]
    public class RolesController : BaseController
    {
        private readonly IRoleService _roleService;
        private readonly IRoleTemplateService _roleTemplateService;
        private readonly IPermissionService _permissionService;
        private readonly IModulePermissionService _modulePermissionService;

        public RolesController()
        {
            _roleService = BuildRoleService();
            _roleTemplateService = BuildRoleTemplateService();
            _permissionService = BuildPermissionService();
            _modulePermissionService = DependencyResolver.Current.GetService<IModulePermissionService>();
        }

        public ActionResult Index(string search = null, bool? isActive = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var pageResult = _roleService.GetListPage(search, isActive, sort, direction, page, pageSize);

            ViewBag.StatusFilter = new SelectList(new[]
            {
                new { Value = "", Text = "All statuses" },
                new { Value = "true", Text = "Active" },
                new { Value = "false", Text = "Inactive" }
            }, "Value", "Text", isActive.HasValue ? isActive.Value.ToString().ToLowerInvariant() : string.Empty);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(ToListPage(pageResult));
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var role = _roleService.GetById(id);
            if (role == null)
            {
                return HttpNotFound();
            }

            var assignedPermissionIds = UnitOfWork.Repository<RolePermission>()
                .Find(x => x.RoleId == id)
                .Select(x => x.PermissionId)
                .ToList();

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PermissionGroups = _modulePermissionService != null
                ? _modulePermissionService.GetModuleGroupedPermissions(assignedPermissionIds)
                : _permissionService.GetAll()
                    .Where(x => assignedPermissionIds.Contains(x.Id))
                    .GroupBy(x => x.Module)
                    .Select(g => new PermissionGroupVm { Module = g.Key, Permissions = g.ToList() })
                    .ToList();
            var organizationId = ResolveCurrentOrganizationId();
            ViewBag.UserCount = organizationId.HasValue
                ? DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.Queries.IUserAccountQueryRepository>()
                    .CountUsersForRole(organizationId.Value, id)
                : 0;
            ViewBag.WorkflowNote = ApprovalWorkflowSettingsHelper.GetTypicalRoleWorkflowNote(role.Name);
            ViewBag.OfferRoleTemplateSave = TempData["OfferRoleTemplateSave"] != null;
            ViewBag.RoleTemplateSaveError = TempData["RoleTemplateSaveError"];

            var settings = ApprovalWorkflowSettingsHelper.ToDictionary(
                UnitOfWork.Repository<SystemSetting>().GetAll());
            ViewBag.ApprovalStageProcesses = ApprovalWorkflowSettingsHelper.GetProcessesUsingRole(id, settings);
            return View(role);
        }

        [PermissionAuthorize("Roles.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(BuildCreateEditVm(null));
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
                return View(BuildCreateEditVm(model));
            }

            var roleId = _roleService.Create(model);
            TempData["Message"] = "Role created successfully.";
            TempData["Guidance"] = "Review the assigned permissions, then assign this role to users or save it as a reusable template.";
            TempData["OfferRoleTemplateSave"] = true;
            return RedirectToAction("Details", new { id = roleId, returnUrl = ViewBag.ReturnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Roles.Create")]
        public ActionResult SaveTemplate(RoleTemplateSaveVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.RoleId });
            if (model == null || model.RoleId <= 0)
            {
                TempData["Message"] = "Role template could not be saved.";
                return RedirectToAction("Details", new { id = model == null ? 0 : model.RoleId, returnUrl = ViewBag.ReturnUrl });
            }

            if (string.IsNullOrWhiteSpace(model.TemplateName))
            {
                TempData["OfferRoleTemplateSave"] = true;
                TempData["RoleTemplateSaveError"] = "Template name is required.";
                return RedirectToAction("Details", new { id = model.RoleId, returnUrl = ViewBag.ReturnUrl });
            }

            try
            {
                _roleTemplateService.CreateFromRole(model.RoleId, model.TemplateName);
                TempData["Message"] = "Role template \"" + model.TemplateName.Trim() + "\" saved successfully.";
            }
            catch (BusinessException ex)
            {
                TempData["OfferRoleTemplateSave"] = true;
                TempData["RoleTemplateSaveError"] = ex.Message;
            }

            return RedirectToAction("Details", new { id = model.RoleId, returnUrl = ViewBag.ReturnUrl });
        }

        [PermissionAuthorize("Roles.Create")]
        public JsonResult RolePermissions(int id)
        {
            try
            {
                return Json(new { permissionIds = _roleService.GetPermissionIds(id) }, JsonRequestBehavior.AllowGet);
            }
            catch (BusinessException ex)
            {
                Response.StatusCode = 400;
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [PermissionAuthorize("Roles.Create")]
        public JsonResult TemplatePermissions(int id)
        {
            try
            {
                return Json(new { permissionIds = _roleTemplateService.GetPermissionIds(id) }, JsonRequestBehavior.AllowGet);
            }
            catch (BusinessException ex)
            {
                Response.StatusCode = 400;
                return Json(new { error = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [PermissionAuthorize("Roles.Edit")]
        public ActionResult Edit(int id, string returnUrl = null)
        {
            var role = _roleService.GetById(id);
            if (role == null)
            {
                return HttpNotFound();
            }

            var selected = UnitOfWork.Repository<RolePermission>().Find(x => x.RoleId == id).Select(x => x.PermissionId).ToList();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            return View(new RoleCreateEditVm
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole,
                SelectedPermissionIds = selected,
                PermissionGroups = _modulePermissionService != null
                    ? _modulePermissionService.GetModuleGroupedPermissions()
                    : _permissionService.GetGroupedPermissions()
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
                model.PermissionGroups = GetPermissionGroups();
                return View(model);
            }

            _roleService.Update(model);
            TempData["Message"] = "Role updated successfully.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }

        private RoleCreateEditVm BuildCreateEditVm(RoleCreateEditVm model)
        {
            model = model ?? new RoleCreateEditVm();
            model.PermissionGroups = GetPermissionGroups();
            model.RoleTemplates = _roleTemplateService.GetTemplates();
            model.CopySourceRoles = BuildRoleCopySources();
            return model;
        }

        private IList<RolePermissionCopyVm> BuildRoleCopySources()
        {
            var roles = _roleService.GetRoles().Where(x => x.IsActive).OrderBy(x => x.Name).ToList();
            if (roles.Count == 0)
            {
                return new List<RolePermissionCopyVm>();
            }

            var roleIds = roles.Select(x => x.Id).ToList();
            var permissionCounts = UnitOfWork.Repository<RolePermission>()
                .Find(x => roleIds.Contains(x.RoleId))
                .GroupBy(x => x.RoleId)
                .ToDictionary(g => g.Key, g => g.Count());

            return roles.Select(role => new RolePermissionCopyVm
            {
                Id = role.Id,
                Name = role.Name,
                PermissionCount = permissionCounts.ContainsKey(role.Id) ? permissionCounts[role.Id] : 0
            }).ToList();
        }

        private IList<PermissionGroupVm> GetPermissionGroups()
        {
            return _modulePermissionService != null
                ? _modulePermissionService.GetModuleGroupedPermissions()
                : _permissionService.GetGroupedPermissions().ToList();
        }
    }
}
