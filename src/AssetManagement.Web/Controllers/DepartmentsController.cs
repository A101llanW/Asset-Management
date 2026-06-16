using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
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

        public ActionResult Index(string search = null, string status = "active", string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
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

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "code":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.Code) : items.OrderBy(x => x.Code);
                    break;
                case "status":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.IsActive) : items.OrderBy(x => x.IsActive);
                    break;
                default:
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.Name) : items.OrderBy(x => x.Name);
                    sort = "name";
                    break;
            }

            SetListSortViewBag(sort, direction);
            return View(BuildListPage(items, search, sort, direction, page, pageSize));
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
            return View(new DepartmentVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(DepartmentVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var departmentId = _departmentService.Create(model);
            TempData["Message"] = "Department created.";
            TempData["Guidance"] = "Next step: review the department details and then add users or assign assets to this department.";
            return RedirectToAction("Details", new { id = departmentId, returnUrl = ViewBag.ReturnUrl });
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
    }
}
