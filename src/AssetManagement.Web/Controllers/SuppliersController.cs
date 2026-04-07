using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Suppliers.View")]
    public class SuppliersController : BaseController
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController()
        {
            _supplierService = BuildSupplierService();
        }

        public ActionResult Index()
        {
            return View(_supplierService.GetAll());
        }

        [PermissionAuthorize("Suppliers.Create")]
        public ActionResult Create()
        {
            return View(new SupplierVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Create")]
        public ActionResult Create(SupplierVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _supplierService.Create(model);
            TempData["Message"] = "Supplier created.";
            return RedirectToAction("Index");
        }

        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult Edit(int id)
        {
            var model = _supplierService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult Edit(SupplierVm model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _supplierService.Update(model);
            TempData["Message"] = "Supplier updated.";
            return RedirectToAction("Index");
        }
    }
}
