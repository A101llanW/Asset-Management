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
            var suppliers = _supplierService.GetAll().Where(x => x.IsActive).ToList();
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
            var receiveDetail = _receivingService.GetReceiveDetail(id);
            ViewBag.ReceiveDetail = receiveDetail;
            ViewBag.AssignableAssetIds = ResolveAssignableAssetIdsForPurchase(receiveDetail);
            ViewBag.OrderStatus = _purchaseService.GetOrderStatus(id);
            return View(model);
        }

        private System.Collections.Generic.IList<int> ResolveAssignableAssetIdsForPurchase(PurchaseReceiveDetailVm detail)
        {
            if (detail == null || detail.Receivings == null || detail.Receivings.Count == 0)
            {
                return new System.Collections.Generic.List<int>();
            }

            var ids = detail.Receivings
                .Where(x => x != null && x.AssetId > 0)
                .Select(x => x.AssetId)
                .Distinct()
                .ToList();
            if (ids.Count == 0)
            {
                return ids;
            }

            return UnitOfWork.Repository<Asset>().GetAll()
                .Where(a => a.IsActive && ids.Contains(a.Id) && a.CurrentStatus == AssetStatus.InStore)
                .ToList()
                .Where(a => string.IsNullOrWhiteSpace(a.CurrentCustodianId))
                .Select(a => a.Id)
                .OrderBy(a => a)
                .ToList();
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
            if (detail != null && detail.RequiresTypeSelection
                && (!model.AssetTypeId.HasValue || model.AssetTypeId.Value <= 0)
                && (!model.AssetSubTypeId.HasValue || model.AssetSubTypeId.Value <= 0))
            {
                ModelState.AddModelError("AssetTypeId", "Select an asset type before receiving. Classification cannot be deferred when this purchase has no requisition/catalog type context.");
            }
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
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            if (!purchaseRequestId.HasValue)
            {
                // rel1009i: a PO is always raised from an approved requisition; start with the picker.
                PopulateRequisitionPicker();
                PopulateLookups(null);
                return View(new PurchaseRecordVm { PurchaseDate = System.DateTime.UtcNow, Currency = GetDefaultCurrencyCode() });
            }

            var req = UnitOfWork.Repository<PurchaseRequest>().GetById(purchaseRequestId.Value);
            if (req == null || !IsRequisitionVisible(req.Id))
            {
                TempData["Error"] = "That requisition was not found or is outside your scope.";
                return RedirectToAction("Create", new { returnUrl });
            }

            string raiserBlock;
            if (!CanCurrentUserRaisePurchaseOrder(req, out raiserBlock))
            {
                // "Purchase order raised by" configured for this requisition's flow and this user is not it.
                TempData["Error"] = raiserBlock;
                return RedirectToAction("Details", "PurchaseRequests", new { id = req.Id, returnUrl = returnUrl });
            }

            var model = _purchaseService.BuildFromRequisition(req.Id);
            if (model == null)
            {
                TempData["Error"] = "Only approved requisitions can be ordered.";
                return RedirectToAction("Details", "PurchaseRequests", new { id = req.Id, returnUrl = returnUrl });
            }

            if (model.Lines.Sum(x => x.Outstanding) <= 0)
            {
                TempData["Message"] = "Everything on this requisition is already on purchase orders.";
                return RedirectToAction("Details", "PurchaseRequests", new { id = req.Id, returnUrl = returnUrl });
            }

            model.Currency = string.IsNullOrWhiteSpace(model.Currency) ? GetDefaultCurrencyCode() : model.Currency;
            PopulateLookups(model);
            ViewBag.PurchaseRequestId = req.Id;
            ViewBag.ItemDescription = req.ItemDescription;
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
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            model = model ?? new PurchaseRecordVm();
            if (!model.PurchaseRequestId.HasValue || !IsRequisitionVisible(model.PurchaseRequestId.Value))
            {
                ModelState.AddModelError("", model.PurchaseRequestId.HasValue
                    ? "That requisition was not found or is outside your scope."
                    : Application.Services.PurchaseService.RequisitionRequiredMessage);
                model.PurchaseRequestId = null;
                PopulateRequisitionPicker();
                PopulateLookups(model);
                return View(model);
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Server-side PO-raiser, scope and over-ordering enforcement live in the service.
                    var purchaseId = _purchaseService.Create(model, User.GetUserId(), GetCurrentUserRoleId());
                    TempData["Message"] = "Purchase order created.";
                    TempData["Guidance"] = "Next step: when the goods arrive, use Receive on this purchase order.";
                    return RedirectToAction("Details", new { id = purchaseId, returnUrl = ViewBag.ReturnUrl });
                }
                catch (BusinessException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            RestoreDraftLines(model);
            PopulateLookups(model);
            ViewBag.PurchaseRequestId = model.PurchaseRequestId;
            ViewBag.ItemDescription = UnitOfWork.Repository<PurchaseRequest>().GetById(model.PurchaseRequestId.Value)?.ItemDescription;
            return View(model);
        }

        /// <summary>rel1009i: the requisition is visible to this user (same org/department scope as the Requisitions list).</summary>
        private bool IsRequisitionVisible(int purchaseRequestId)
        {
            return _purchaseRequestService.GetAll().Any(x => x.Id == purchaseRequestId);
        }

        private void PopulateRequisitionPicker()
        {
            var visibleIds = _purchaseRequestService.GetAll().Select(x => x.Id).ToList();
            var options = _purchaseService.GetRequisitionsReadyForPurchaseOrder(visibleIds)
                .Where(option =>
                {
                    var req = UnitOfWork.Repository<PurchaseRequest>().GetById(option.Id);
                    string ignored;
                    return req != null && CanCurrentUserRaisePurchaseOrder(req, out ignored);
                })
                .ToList();
            ViewBag.RequisitionOptions = options;
        }

        /// <summary>After a failed save: the requisition's lines again (limits from the server) with the posted entries.</summary>
        private void RestoreDraftLines(PurchaseRecordVm model)
        {
            var draft = _purchaseService.BuildFromRequisition(model.PurchaseRequestId.Value);
            if (draft == null)
            {
                return;
            }

            var posted = model.Lines ?? new System.Collections.Generic.List<PurchaseOrderLineInputVm>();
            for (var i = 0; i < draft.Lines.Count; i++)
            {
                var line = draft.Lines[i];
                var input = posted.FirstOrDefault(x => x != null && x.PurchaseRequestLineId == line.PurchaseRequestLineId)
                    ?? (i < posted.Count ? posted[i] : null);
                if (input == null)
                {
                    continue;
                }

                line.Quantity = input.Quantity;
                line.UnitCost = input.UnitCost;
                if (!line.ClassificationLocked)
                {
                    line.AssetTypeId = input.AssetTypeId;
                    line.AssetSubTypeId = input.AssetSubTypeId;
                }
            }

            model.Lines = draft.Lines;
            model.PurchaseRequestNumber = draft.PurchaseRequestNumber;
            if (string.IsNullOrWhiteSpace(model.Currency))
            {
                model.Currency = draft.Currency ?? GetDefaultCurrencyCode();
            }
        }

        private bool CanCurrentUserRaisePurchaseOrder(PurchaseRequest request, out string message)
        {
            message = null;
            var config = Application.Services.PurchaseOrderRaiserRules.ResolveForRequest(UnitOfWork, request);
            if (Application.Services.PurchaseOrderRaiserRules.CanRaise(
                    config,
                    User.GetUserId(),
                    GetCurrentUserRoleId(),
                    Application.Services.PurchaseOrderRaiserRules.ResolveRaiserScope(UnitOfWork, BuildUserService(), config, request == null ? (int?)null : request.DepartmentId)))
            {
                return true;
            }

            var label = Application.Services.PurchaseOrderRaiserRules.Describe(UnitOfWork, BuildUserService(), config);
            message = "Only " + (string.IsNullOrWhiteSpace(label) ? "the configured purchase order raiser" : label)
                + " can raise the purchase order for this requisition.";
            return false;
        }

        private void PopulateLookups(PurchaseRecordVm model)
        {
            ViewBag.Suppliers = BuildSupplierSelectList(model?.SupplierId);

            // rel1009i: optional per-line classification on the PO (what Receiving auto-fills).
            var categories = UnitOfWork.Repository<AssetCategory>().GetAll().Where(x => x.IsActive)
                .GroupBy(x => x.Id).ToDictionary(g => g.Key, g => g.First().Name);
            var types = UnitOfWork.Repository<AssetType>().GetAll().Where(x => x.IsActive).ToList();
            ViewBag.LineTypeOptions = types
                .Select(t => new AssetClassOptionVm
                {
                    Id = t.Id,
                    Name = t.Name,
                    GroupName = categories.ContainsKey(t.AssetCategoryId) ? categories[t.AssetCategoryId] : "Other"
                })
                .OrderBy(x => x.GroupName).ThenBy(x => x.Name)
                .ToList();
            var typeNames = types.ToDictionary(t => t.Id, t => t.Name);
            ViewBag.LineSubTypeOptions = UnitOfWork.Repository<AssetSubType>().GetAll()
                .Where(x => x.IsActive && typeNames.ContainsKey(x.AssetTypeId))
                .Select(st => new AssetClassOptionVm { Id = st.Id, Name = st.Name, ParentId = st.AssetTypeId, GroupName = typeNames[st.AssetTypeId] })
                .OrderBy(x => x.GroupName).ThenBy(x => x.Name)
                .ToList();
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
            // Prefer grouped Web lookup VMs for receive classification selector.
            ViewBag.AssetTypeOptions = BuildAssetTypeLookupList(activeOnly: true);
            ViewBag.SubTypeLookupUrl = TenantUrlHelper.TenantRouteUrl(Url, "Lookup", "AssetSubTypes");
            ViewBag.SubTypeByTypeUrl = TenantUrlHelper.TenantRouteUrl(Url, "ByType", "AssetSubTypes");
            ViewBag.SubTypeCreateUrl = TenantUrlHelper.TenantRouteUrl(Url, "CreateFromAsset", "AssetSubTypes");
        }
    }
}
