using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Purchases.View")]
    public class PurchasesController : BaseController
    {
        private readonly IPurchaseService _purchaseService;
        private readonly IReceivingService _receivingService;
        private readonly ISupplierService _supplierService;
        private readonly ISupplierCatalogService _supplierCatalogService;
        private readonly IPurchaseRequestService _purchaseRequestService;

        public PurchasesController()
        {
            _purchaseService = BuildPurchaseService();
            _receivingService = BuildReceivingService();
            _supplierService = BuildSupplierService();
            _supplierCatalogService = BuildSupplierCatalogService();
            _purchaseRequestService = BuildPurchaseRequestService();
        }

        public ActionResult Index(string search = null, int? supplierId = null, string sort = "date", string direction = "desc", int page = 1, int pageSize = 10)
        {
            var suppliers = _supplierService.GetAll().ToList();
            var items = _purchaseService.GetAll();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                items = items.Where(x => (x.PurchaseOrderNumber ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.InvoiceNumber ?? string.Empty).ToLowerInvariant().Contains(term)
                    || (x.SupplierName ?? string.Empty).ToLowerInvariant().Contains(term));
            }

            if (supplierId.HasValue)
            {
                items = items.Where(x => x.SupplierId == supplierId.Value);
            }

            switch ((sort ?? string.Empty).ToLowerInvariant())
            {
                case "supplier":
                    items = string.Equals(direction, "asc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderBy(x => x.SupplierName) : items.OrderByDescending(x => x.SupplierName);
                    break;
                case "total":
                    items = string.Equals(direction, "asc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderBy(x => x.TotalCost) : items.OrderByDescending(x => x.TotalCost);
                    break;
                default:
                    items = string.Equals(direction, "asc", System.StringComparison.OrdinalIgnoreCase) ? items.OrderBy(x => x.PurchaseDate) : items.OrderByDescending(x => x.PurchaseDate);
                    sort = "date";
                    break;
            }

            ViewBag.SupplierFilter = new SelectList(suppliers, "Id", "SupplierName", supplierId);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(BuildListPage(items, search, sort, direction, page, pageSize));
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _purchaseService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.ReceiveDetail = _receivingService.GetReceiveDetail(id);
            return View(model);
        }

        [PermissionAuthorize("Assets.Receive")]
        public ActionResult Receive(int id, int? assetId = null, string returnUrl = null)
        {
            var detail = _receivingService.GetReceiveDetail(id);
            if (detail == null)
            {
                return HttpNotFound();
            }

            if (detail.RemainingQuantity <= 0)
            {
                TempData["Message"] = "This purchase is already fully received.";
                return RedirectToAction("Details", new { id, returnUrl });
            }

            var lookup = _receivingService.GetReceiveAssetLookup(id, assetId);
            var model = new AssetReceiveVm
            {
                PurchaseRecordId = id,
                AssetId = lookup.SelectedAssetId ?? 0,
                ReceivedDate = System.DateTime.UtcNow,
                QuantityReceived = 1
            };

            PopulateReceiveLookups(model, lookup);
            ViewBag.ReceiveDetail = detail;
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "Purchases", new { id });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Receive")]
        public ActionResult Receive(AssetReceiveVm model, string returnUrl = null)
        {
            var detail = _receivingService.GetReceiveDetail(model.PurchaseRecordId);
            if (detail == null)
            {
                return HttpNotFound();
            }

            PopulateReceiveLookups(model, _receivingService.GetReceiveAssetLookup(model.PurchaseRecordId, model.AssetId > 0 ? model.AssetId : (int?)null));
            ViewBag.ReceiveDetail = detail;
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "Purchases", new { id = model.PurchaseRecordId });

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _receivingService.Receive(model, User.GetUserId());
                TempData["Message"] = "Asset received against purchase record.";
                return RedirectToAction("Details", new { id = model.PurchaseRecordId, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(string returnUrl = null, int? purchaseRequestId = null)
        {
            var model = new PurchaseRecordVm
            {
                PurchaseDate = System.DateTime.UtcNow,
                Currency = GetDefaultCurrencyCode(),
                PurchaseRequestId = purchaseRequestId
            };

            if (purchaseRequestId.HasValue)
            {
                var req = UnitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId.Value);
                if (req != null && req.ApprovalStatus == ApprovalStatus.Approved)
                {
                    model.Currency = req.Currency ?? model.Currency;
                    model.Quantity = req.Quantity;
                    model.UnitCost = req.EstimatedUnitCost;
                    model.TotalCost = req.EstimatedUnitCost * req.Quantity;
                }
            }

            PopulateLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseRequestId = purchaseRequestId;
            ViewBag.ItemDescription = purchaseRequestId.HasValue
                ? UnitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId.Value)?.ItemDescription
                : null;
            return View(model);
        }

        public JsonResult SupplierPriceComparison(int? purchaseRequestId, string itemDescription)
        {
            var comparison = _supplierCatalogService.GetPriceComparison(purchaseRequestId, itemDescription);
            return Json(comparison, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(PurchaseRecordVm model, string returnUrl = null)
        {
            PopulateLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseRequestId = model.PurchaseRequestId;
            ViewBag.ItemDescription = model.PurchaseRequestId.HasValue
                ? UnitOfWork.Repository<PurchaseRequest>().GetById(model.PurchaseRequestId.Value)?.ItemDescription
                : null;
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var purchaseId = _purchaseService.Create(model);
                TempData["Message"] = "Purchase record created.";
                TempData["Guidance"] = "Next step: review the purchase details and verify the supplier, invoice, and cost values.";
                return RedirectToAction("Details", new { id = purchaseId, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private void PopulateLookups(PurchaseRecordVm model)
        {
            ViewBag.Suppliers = BuildSupplierSelectList(model?.SupplierId);
        }

        private void PopulateReceiveLookups(AssetReceiveVm model, ReceiveAssetLookupVm lookup)
        {
            var selectedAssetId = model.AssetId > 0 ? model.AssetId : lookup?.SelectedAssetId;
            ViewBag.Assets = new SelectList(lookup?.Assets ?? new System.Collections.Generic.List<ReceiveAssetOptionVm>(), "Id", "Label", selectedAssetId);
            if (selectedAssetId.HasValue && selectedAssetId.Value > 0)
            {
                model.AssetId = selectedAssetId.Value;
            }
        }
    }
}
