using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Departments.View")]
    public class DepartmentsController : BaseController
    {
        private readonly IDepartmentService _departmentService;

        public DepartmentsController()
        {
            _departmentService = BuildDepartmentService();
        }

        public ActionResult Index(string search = null, string status = "active", string view = "tree")
        {
            var items = FilterBySearch(_departmentService.GetAll(), search, (x, term) =>
                (x.Name ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.Code ?? string.Empty).ToLowerInvariant().Contains(term)
                || (x.Description ?? string.Empty).ToLowerInvariant().Contains(term));

            switch ((status ?? "active").ToLowerInvariant())
            {
                case "all":
                    break;
                case "inactive":
                    items = items.Where(x => !x.IsActive);
                    break;
                default:
                    status = "active";
                    items = items.Where(x => x.IsActive);
                    break;
            }

            ViewBag.StatusFilter = status;
            ViewBag.ViewMode = string.Equals(view, "list", StringComparison.OrdinalIgnoreCase) ? "list" : "tree";
            ViewBag.Search = search;
            ViewBag.TreeSections = _departmentService.GetTreeSections()
                .Select(section => new DepartmentTreeSectionVm
                {
                    Title = section.Title,
                    Items = section.Items
                        .Where(item => items.Any(x => x.Id == item.Id || item.Children.Any(child => child.Id == x.Id)))
                        .ToList()
                })
                .Where(section => section.Items.Any())
                .ToList();

            return View(items.OrderBy(x => x.Name).ToList());
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _departmentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.ActiveUserCount = BuildUserService().GetAll().Count(x => x.DepartmentId == id && x.IsActive);
            ViewBag.AssetCount = BuildAssetService().CountAssets(new AssetFilterVm { DepartmentId = model.Id });
            return View(model);
        }

        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.SetupModes = BuildSetupModeSelectList(DepartmentService.SetupModeNormal);
            ViewBag.AdminParentDepartments = BuildAdminParentDepartmentSelectList(null);
            return View(new DepartmentCreateVm { SetupMode = DepartmentService.SetupModeNormal });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(DepartmentCreateVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.SetupModes = BuildSetupModeSelectList(model == null ? DepartmentService.SetupModeNormal : model.SetupMode);
            ViewBag.AdminParentDepartments = BuildAdminParentDepartmentSelectList(model == null ? null : model.ParentDepartmentId);
            if (model == null)
            {
                ModelState.AddModelError("", "Department details are required.");
                return View(new DepartmentCreateVm { SetupMode = DepartmentService.SetupModeNormal });
            }

            try
            {
                var departmentId = _departmentService.CreateFromWizard(model);
                TempData["Message"] = "Department created.";
                TempData["Guidance"] = "Next step: review the department details and then add users or assign assets to this department.";
                return RedirectToAction("Details", new { id = departmentId, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(int id, string returnUrl = null)
        {
            var model = _departmentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(DepartmentVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _departmentService.Update(model);
            TempData["Message"] = "Department updated.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }

        private static SelectList BuildSetupModeSelectList(string selected)
        {
            var items = new[]
            {
                new { Value = DepartmentService.SetupModeNormal, Text = "Normal (administrative)" },
                new { Value = DepartmentService.SetupModeSubDepartment, Text = "Sub-unit under admin department" },
                new { Value = DepartmentService.SetupModeGradeStreams, Text = "Grade with class streams" },
                new { Value = DepartmentService.SetupModeBulkGrades, Text = "Bulk grades 1–6" }
            };
            return new SelectList(items, "Value", "Text", selected);
        }

        private SelectList BuildAdminParentDepartmentSelectList(int? selectedParentDepartmentId)
        {
            var parents = _departmentService.GetAll()
                .Where(x => x.IsActive
                    && x.DepartmentKind == DepartmentKind.Administrative
                    && !x.ParentDepartmentId.HasValue)
                .OrderBy(x => x.Name)
                .ToList();
            return new SelectList(parents, "Id", "Name", selectedParentDepartmentId);
        }
    }
}
