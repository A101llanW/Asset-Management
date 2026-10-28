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
            // rel1014a: every asset received on this purchase (assigned or not) for "Send to Bulk Label Print".
            ViewBag.ReceivedAssetCount = receiveDetail == null || receiveDetail.Receivings == null
                ? 0
                : receiveDetail.Receivings.Where(x => x != null && x.AssetId > 0).Select(x => x.AssetId).Distinct().Count();
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

        /// <summary>rel1009i: receiving starts from a PO; from a requisition, go to its only open PO (else its PO list).</summary>
        [PermissionAuthorize("Assets.Receive")]
        public ActionResult ReceiveForRequisition(int purchaseRequestId, string returnUrl = null)
        {
            var summary = _purchaseService.GetRequisitionOrderSummary(purchaseRequestId);
            var open = summary == null ? new System.Collections.Generic.List<RequisitionLinkedOrderVm>() : summary.Orders.Where(x => x.Received < x.Quantity).ToList();
            if (open.Count == 1)
            {
                return RedirectToAction("Receive", new { id = open[0].PurchaseRecordId, returnUrl });
            }

            TempData["Message"] = open.Count == 0
                ? "This requisition has no purchase order with goods still to receive."
                : "This requisition has several open purchase orders. Choose one to receive.";
            return RedirectToAction("Details", "PurchaseRequests", new { id = purchaseRequestId, returnUrl });
        }

        [PermissionAuthorize("Assets.Receive")]
        public ActionResult Receive(int id, string returnUrl = null)
        {
            var model = _receivingService.GetReceiveOrder(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            if (model.Outstanding <= 0)
            {
                TempData["Message"] = "This purchase order is already fully received.";
                return RedirectToAction("Details", new { id, returnUrl });
            }

            PopulateReceiveOrderLookups();
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "Purchases", new { id });
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Receive")]
        public ActionResult Receive(ReceiveOrderVm model, string returnUrl = null)
        {
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Details", "Purchases", new { id = model.PurchaseRecordId });
            if (ModelState.IsValid)
            {
                try
                {
                    var result = _receivingService.ReceiveOrder(model, User.GetUserId());
                    var tags = string.Join(", ", result.CreatedAssets.Select(x => x.AssetTag).Where(x => !string.IsNullOrWhiteSpace(x)));
                    TempData["Message"] = result.CreatedAssets.Count == 1
                        ? "Asset " + tags + " created and received."
                        : result.CreatedAssets.Count + " assets created and received: " + tags + ".";
                    TempData["CreatedAssetIds"] = string.Join(",", result.CreatedAssets.Select(x => x.AssetId));
                    return RedirectToAction("Details", new { id = model.PurchaseRecordId, returnUrl = ViewBag.ReturnUrl });
                }
                catch (BusinessException ex)
                {
                    ModelState.AddModelError("", ex.Message);
                }
            }

            // Redisplay: quantities and classification from the server, the user's entries kept.
            var fresh = _receivingService.GetReceiveOrder(model.PurchaseRecordId);
            if (fresh == null)
            {
                return HttpNotFound();
            }

            var posted = model.Lines ?? new System.Collections.Generic.List<ReceiveOrderLineVm>();
            foreach (var line in fresh.Lines)
            {
                var input = posted.FirstOrDefault(x => x != null && x.PurchaseOrderLineId == line.PurchaseOrderLineId);
                if (input == null)
                {
                    continue;
                }

                line.QuantityReceived = input.QuantityReceived;
                if (!string.IsNullOrWhiteSpace(input.ItemName))
                {
                    line.ItemName = input.ItemName;
                }

                line.OverrideAssetTypeId = input.OverrideAssetTypeId;
                line.OverrideAssetSubTypeId = input.OverrideAssetSubTypeId;
                // C3: reopen the Edit panel when the receiver had changed a classified line's type / sub-type.
                // Decision B: a panel the user opened stays open on redisplay, blanks included.
                line.ClassificationEdited = !line.NeedsClassification
                    && (input.ClassificationEditOpened
                        || (input.OverrideAssetTypeId.HasValue && input.OverrideAssetTypeId.Value > 0
                            && (input.OverrideAssetTypeId != line.AssetTypeId || input.OverrideAssetSubTypeId != line.AssetSubTypeId)));
                line.SerialNumbersText = input.SerialNumbersText;
                line.TagMode = input.TagMode;
                line.AssetTagsText = input.AssetTagsText;
                line.Condition = input.Condition;
                line.DestinationKind = input.DestinationKind;
                line.DestinationDepartmentId = input.DestinationDepartmentId;
            }

            fresh.ReceivedDate = model.ReceivedDate == default(System.DateTime) ? fresh.ReceivedDate : model.ReceivedDate;
            fresh.Notes = model.Notes;
            PopulateReceiveOrderLookups();
            return View(fresh);
        }

        private void PopulateReceiveOrderLookups()
        {
            PopulateLookups(null);
            // Full-depth "Parent > Child" department / room picker (rel1009g tree).
            ViewBag.DepartmentTree = BuildDepartmentTreeSelectList();
            ViewBag.ConditionOptions = BuildAssetConditionSelectList(null);
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
                if (!line.ClassificationPrefilled)
                {
                    line.AssetTypeId = input.AssetTypeId;
                    line.AssetSubTypeId = input.AssetSubTypeId;
                }
                else if (input.AssetTypeId.HasValue && input.AssetTypeId.Value > 0
                    && (input.AssetTypeId != line.PrefilledAssetTypeId || input.AssetSubTypeId != line.PrefilledAssetSubTypeId))
                {
                    // C3: the user had pressed Edit and changed it - reopen the editor with their choice.
                    line.AssetTypeId = input.AssetTypeId;
                    line.AssetSubTypeId = input.AssetSubTypeId;
                    line.ClassificationEdited = true;
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
                    Application.Services.PurchaseOrderRaiserRules.ResolveRaiserScope(UnitOfWork, BuildUserService(), config, request == null ? (int?)null : request.DepartmentId, request != null && Application.Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(request.ApprovalFlowSource))))
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
