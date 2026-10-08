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
            var listOrganizationId = ResolveCurrentOrganizationId();
            ViewBag.RoleDepartmentAvailable = RoleDepartments.IsAvailable();
            ViewBag.RoleDepartmentNames = listOrganizationId.HasValue
                ? RoleDepartments.GetRoleDepartmentNames(listOrganizationId.Value)
                : new Dictionary<int, string>();
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
            SetRoleDepartmentViewBag(null);
            return View(BuildCreateEditVm(null));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Roles.Create")]
        public ActionResult Create(RoleCreateEditVm model, string returnUrl = null)
        {
            model.SelectedPermissionIds = model.SelectedPermissionIds ?? new System.Collections.Generic.List<int>();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            SetRoleDepartmentViewBag(model.DepartmentId);
            ValidateRoleDepartment(model, false);
            if (!ModelState.IsValid)
            {
                return View(BuildCreateEditVm(model));
            }

            var roleId = _roleService.Create(model);
            SaveRoleDepartment(roleId, model.DepartmentId, false);
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
            var roleDepartmentId = RoleDepartments.GetRoleDepartmentId(role.Id);
            SetRoleDepartmentViewBag(roleDepartmentId);
            return View(new RoleCreateEditVm
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole,
                DepartmentId = roleDepartmentId,
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
            SetRoleDepartmentViewBag(model.DepartmentId);
            var existingRole = _roleService.GetById(model.Id);
            ValidateRoleDepartment(model, existingRole != null && existingRole.IsSystemRole);
            if (!ModelState.IsValid)
            {
                model.PermissionGroups = GetPermissionGroups();
                return View(model);
            }

            if (!model.IsActive && IsCurrentUsersRole(model.Id))
            {
                ModelState.AddModelError("", "You cannot deactivate your own role.");
                model.PermissionGroups = GetPermissionGroups();
                return View(model);
            }

            _roleService.Update(model);
            SaveRoleDepartment(model.Id, model.DepartmentId, true);
            TempData["Message"] = "Role updated successfully.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }

        private AssetManagement.Infrastructure.Identity.RoleDepartmentScopeRepository _roleDepartments;

        /// <summary>Roles.DepartmentId (migration 096): null = all departments / organization-wide.</summary>
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

        private void SetRoleDepartmentViewBag(int? selectedDepartmentId)
        {
            ViewBag.RoleDepartmentAvailable = RoleDepartments.IsAvailable();
            ViewBag.RoleDepartments = GetActiveDepartments()
                .OrderBy(d => d.Name)
                .ThenBy(d => d.ParentDepartmentName)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(d.ParentDepartmentName) ? d.Name : d.Name + " (" + d.ParentDepartmentName + ")",
                    Selected = selectedDepartmentId.HasValue && d.Id == selectedDepartmentId.Value
                })
                .ToList();
        }

        /// <summary>A role department must be an active department of this organization; system roles stay organization-wide.</summary>
        private void ValidateRoleDepartment(RoleCreateEditVm model, bool isSystemRole)
        {
            if (model == null || !model.DepartmentId.HasValue)
            {
                return;
            }

            var organizationId = ResolveCurrentOrganizationId();
            if (isSystemRole)
            {
                ModelState.AddModelError("DepartmentId", "System roles apply to all departments.");
            }
            else if (!RoleDepartments.IsAvailable())
            {
                ModelState.AddModelError("DepartmentId", "Role departments are not available until database migration 096 is applied.");
            }
            else if (!organizationId.HasValue || !RoleDepartments.IsActiveTenantDepartment(model.DepartmentId.Value, organizationId.Value))
            {
                ModelState.AddModelError("DepartmentId", "Choose an active department of this organization, or All departments.");
            }
        }

        private void SaveRoleDepartment(int roleId, int? departmentId, bool isEdit)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue || !RoleDepartments.IsAvailable())
            {
                return;
            }

            var before = RoleDepartments.GetRoleDepartmentId(roleId);
            if (before == departmentId)
            {
                return;
            }

            RoleDepartments.SetRoleDepartmentId(roleId, organizationId.Value, departmentId);
            var audit = DependencyResolver.Current.GetService<AssetManagement.Application.Contracts.IAuditWriter>();
            if (audit != null)
            {
                audit.Write("Roles.DepartmentScope", nameof(Role), roleId.ToString(),
                    isEdit ? (before.HasValue ? "DepartmentId=" + before.Value : "DepartmentId=all") : null,
                    departmentId.HasValue ? "DepartmentId=" + departmentId.Value : "DepartmentId=all");
            }
        }

        // Role Active flag: users may not deactivate the role they are signed in with.
        private bool IsCurrentUsersRole(int roleId)
        {
            var connectionFactory = DependencyResolver.Current.GetService<AssetManagement.Infrastructure.Persistence.ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return false;
            }

            var currentUser = new AssetManagement.Infrastructure.Identity.UserAccountRepository(connectionFactory)
                .FindById(AssetManagement.Web.Security.CurrentUserExtensions.GetUserId(User));
            return currentUser != null && currentUser.RoleId.HasValue && currentUser.RoleId.Value == roleId;
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

            var roleIdSet = new HashSet<int>(roles.Select(x => x.Id));
            // Avoid repository Find(... Contains ...) — not supported by SQL predicate translator.
            var permissionCounts = UnitOfWork.Repository<RolePermission>()
                .GetAll()
                .Where(x => roleIdSet.Contains(x.RoleId))
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
