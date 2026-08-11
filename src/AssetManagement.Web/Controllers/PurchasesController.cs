using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
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
            var pageResult = _purchaseService.GetListPage(search, supplierId, sort, direction, page, pageSize);

            ViewBag.SupplierFilter = new SelectList(suppliers, "Id", "SupplierName", supplierId);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(ToListPage(pageResult));
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
        public ActionResult Receive(int id, int? assetId = null, string returnUrl = null, bool catalogConfirmed = false)
        {
            var detail = _receivingService.GetReceiveDetail(id, catalogConfirmed);
            if (detail == null)
            {
                return HttpNotFound();
            }

            if (detail.RemainingQuantity <= 0)
            {
                TempData["Message"] = "This purchase is already fully received.";
                return RedirectToAction("Details", new { id, returnUrl });
            }

            var lookup = _receivingService.GetReceiveAssetLookup(id, assetId, catalogConfirmed);
            var model = new AssetReceiveVm
            {
                PurchaseRecordId = id,
                AssetId = lookup.SelectedAssetId ?? 0,
                AssetSubTypeId = detail.AssetSubTypeId,
                CatalogMatchConfirmed = catalogConfirmed,
                ReceivedDate = System.DateTime.UtcNow,
                QuantityReceived = detail.RemainingQuantity
            };

            PopulateReceiveLookups(model, lookup, detail);
            ViewBag.ReceiveDetail = detail;
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "Purchases", new { id });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Receive")]
        public ActionResult Receive(AssetReceiveVm model, string returnUrl = null)
        {
            var detail = _receivingService.GetReceiveDetail(model.PurchaseRecordId, model.CatalogMatchConfirmed);
            if (detail == null)
            {
                return HttpNotFound();
            }

            PopulateReceiveLookups(model, _receivingService.GetReceiveAssetLookup(model.PurchaseRecordId, model.AssetId > 0 ? model.AssetId : (int?)null, model.CatalogMatchConfirmed), detail);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "Purchases", new { id = model.PurchaseRecordId });

            ModelState.Remove("AssetId");
            if (detail.RequisitionDepartmentId.HasValue && detail.RequisitionDepartmentId.Value > 0
                && string.IsNullOrWhiteSpace(model.ReceivePlacementChoice))
            {
                ModelState.AddModelError("ReceivePlacementChoice", "Choose whether received goods go to the requisition department or company custody.");
            }

            if (string.IsNullOrWhiteSpace(model.ConditionOnReceipt))
            {
                ModelState.AddModelError("ConditionOnReceipt", "Condition on receipt is required when creating new assets.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var result = _receivingService.Receive(model, User.GetUserId());
                if (result.CreatedAssets != null && result.CreatedAssets.Any())
                {
                    var tags = string.Join(", ", result.CreatedAssets.Select(x => x.AssetTag).Where(x => !string.IsNullOrWhiteSpace(x)));
                    TempData["Message"] = result.CreatedAssets.Count == 1
                        ? "Asset " + tags + " created and received."
                        : result.CreatedAssets.Count + " assets created and received: " + tags + ".";
                    TempData["CreatedAssetIds"] = string.Join(",", result.CreatedAssets.Select(x => x.AssetId));
                }
                else
                {
                    TempData["Message"] = "Assets received against purchase record.";
                }

                return RedirectToAction("Details", new { id = model.PurchaseRecordId, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        [PermissionAuthorize("Purchases.Edit")]
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
                    if (req.Quantity > 0)
                    {
                        model.Quantity = req.Quantity;
                    }
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
        [PermissionAuthorize("Purchases.Edit")]
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

        private void PopulateReceiveLookups(AssetReceiveVm model, ReceiveAssetLookupVm lookup, PurchaseReceiveDetailVm detail)
        {
            ViewBag.ConditionOptions = BuildAssetConditionSelectList(model?.ConditionOnReceipt);

            var selectedAssetId = model.AssetId > 0 ? model.AssetId : lookup?.SelectedAssetId;
            ViewBag.Assets = new SelectList(lookup?.Assets ?? new System.Collections.Generic.List<ReceiveAssetOptionVm>(), "Id", "Label", selectedAssetId);
            if (selectedAssetId.HasValue && selectedAssetId.Value > 0)
            {
                model.AssetId = selectedAssetId.Value;
            }

            ViewBag.ReceiveDetail = detail ?? _receivingService.GetReceiveDetail(model.PurchaseRecordId, model.CatalogMatchConfirmed);
            int? categoryId = null;
            if (detail != null && detail.ContextAssetTypeId.HasValue)
            {
                var assetType = UnitOfWork.Repository<AssetType>().GetById(detail.ContextAssetTypeId.Value);
                categoryId = assetType?.AssetCategoryId;
            }
            ViewBag.Categories = BuildCategorySelectList(categoryId);
            ViewBag.AssetTypeOptions = UnitOfWork.Repository<AssetType>()
                .GetAll()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .ToList();
            ViewBag.SubTypeLookupUrl = TenantUrlHelper.TenantRouteUrl(Url, "Lookup", "AssetSubTypes");
            ViewBag.SubTypeByTypeUrl = TenantUrlHelper.TenantRouteUrl(Url, "ByType", "AssetSubTypes");
            ViewBag.SubTypeCreateUrl = TenantUrlHelper.TenantRouteUrl(Url, "CreateFromAsset", "AssetSubTypes");
        }
    }
}
