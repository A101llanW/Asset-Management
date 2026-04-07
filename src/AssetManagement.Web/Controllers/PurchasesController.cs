using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Purchases.View")]
    public class PurchasesController : BaseController
    {
        private readonly IPurchaseService _purchaseService;
        private readonly ISupplierService _supplierService;

        public PurchasesController()
        {
            _purchaseService = BuildPurchaseService();
            _supplierService = BuildSupplierService();
        }

        public ActionResult Index()
        {
            return View(_purchaseService.GetAll());
        }

        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create()
        {
            var model = new PurchaseRecordVm
            {
                PurchaseDate = System.DateTime.UtcNow,
                Currency = "USD"
            };

            PopulateLookups(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(PurchaseRecordVm model)
        {
            PopulateLookups(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            _purchaseService.Create(model);
            TempData["Message"] = "Purchase record created.";
            return RedirectToAction("Index");
        }

        private void PopulateLookups(PurchaseRecordVm model)
        {
            ViewBag.Suppliers = new SelectList(_supplierService.GetAll(), "Id", "SupplierName", model?.SupplierId);
        }
    }
}
