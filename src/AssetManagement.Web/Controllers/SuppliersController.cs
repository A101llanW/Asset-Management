using System.Linq;
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

        public ActionResult Index(string search = null, string sort = "name", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var items = _supplierService.GetAll();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => (x.SupplierName ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.ContactPerson ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Email ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.Phone ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "contact":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.ContactPerson) : items.OrderBy(x => x.ContactPerson);
                    break;
                case "status":
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.IsActive) : items.OrderBy(x => x.IsActive);
                    break;
                default:
                    items = string.Equals(direction, "desc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderByDescending(x => x.SupplierName) : items.OrderBy(x => x.SupplierName);
                    sort = "name";
                    break;
            }

            SetListSortViewBag(sort, direction);
            return View(BuildListPage(items, search, sort, direction, page, pageSize));
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _supplierService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseCount = BuildPurchaseService().GetAll().Count(x => x.SupplierId == id);
            ViewBag.AssetCount = BuildAssetService().CountAssets(new AssetFilterVm { SupplierId = id });
            return View(model);
        }

        [PermissionAuthorize("Suppliers.Create")]
        public ActionResult Create(string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            return View(new SupplierVm());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Create")]
        public ActionResult Create(SupplierVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var supplierId = _supplierService.Create(model);
            TempData["Message"] = "Supplier created.";
            TempData["Guidance"] = "Next step: review the supplier details and then use this supplier when creating purchase or asset records.";
            return RedirectToAction("Details", new { id = supplierId, returnUrl = ViewBag.ReturnUrl });
        }

        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult Edit(int id, string returnUrl = null)
        {
            var model = _supplierService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Suppliers.Edit")]
        public ActionResult Edit(SupplierVm model, string returnUrl = null)
        {
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", null, new { id = model.Id });
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _supplierService.Update(model);
            TempData["Message"] = "Supplier updated.";
            return RedirectToReturnUrl(returnUrl, "Details", null, new { id = model.Id });
        }
    }
}
