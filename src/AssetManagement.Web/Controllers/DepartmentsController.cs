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

        public ActionResult Index()
        {
            return View(_departmentService.GetAll());
        }

        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create()
        {
            return View(new DepartmentVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Create")]
        public ActionResult Create(DepartmentVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _departmentService.Create(model);
            TempData["Message"] = "Department created.";
            return RedirectToAction("Index");
        }

        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(int id)
        {
            var model = _departmentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Departments.Edit")]
        public ActionResult Edit(DepartmentVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _departmentService.Update(model);
            TempData["Message"] = "Department updated.";
            return RedirectToAction("Index");
        }
    }
}
