using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Script.Serialization;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [AnyPermissionAuthorize("Purchases.View", "Purchases.Create", "Purchases.Approve")]
    public partial class PurchaseRequestsController : BaseController
    {
        private static readonly string[] AllowedAttachmentExtensions = { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv" };
        private const long MaxAttachmentSizeBytes = 10 * 1024 * 1024;

        private readonly IPurchaseRequestService _purchaseRequestService;
        private readonly IFileStorageProvider _storage;

        public PurchaseRequestsController()
        {
            _purchaseRequestService = BuildPurchaseRequestService();
            _storage = DependencyResolver.Current.GetService<IFileStorageProvider>();
        }

        public ActionResult Index(string search = null, string sort = "created", string direction = "desc", int page = 1, int pageSize = 10)
        {
            var pageResult = _purchaseRequestService.GetListPage(search, sort, direction, page, pageSize);
            ViewBag.Sort = sort;
            ViewBag.Direction = direction;
            return View(ToListPage(pageResult));
        }

        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(string returnUrl = null, int? fromAssetRequestId = null)
        {
            var model = new PurchaseRequestCreateVm
            {
                Currency = GetDefaultCurrencyCode(),
                Quantity = 1,
                RequestForSelf = true,
                Lines = new List<PurchaseRequestLineCreateVm>
                {
                    new PurchaseRequestLineCreateVm { Quantity = 1 }
                }
            };

            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId =>
            {
                if (deptId.HasValue)
                {
                    model.DepartmentId = deptId.Value;
                }
            });

            if (fromAssetRequestId.HasValue)
            {
                var assetRequest = BuildAssetRequestService().GetById(fromAssetRequestId.Value);
                if (assetRequest != null)
                {
                    if (assetRequest.DepartmentId.HasValue)
                    {
                        model.DepartmentId = assetRequest.DepartmentId.Value;
                    }

                    var prefilledDescription = !string.IsNullOrWhiteSpace(assetRequest.RequestedAssetName)
                        ? assetRequest.RequestedAssetName
                        : assetRequest.CategoryName;
                    model.ItemDescription = prefilledDescription;
                    model.Lines = new List<PurchaseRequestLineCreateVm>
                    {
                        new PurchaseRequestLineCreateVm
                        {
                            Description = prefilledDescription,
                            Quantity = 1
                        }
                    };
                    model.Justification = assetRequest.Justification;
                    if (assetRequest.RequestedAssetId.HasValue)
                    {
                        model.TargetAssetId = assetRequest.RequestedAssetId;
                    }
                }
            }

            PopulateCreateLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseApprovalSummary = BuildApprovalProcessSummary(ApprovalProcessCodes.Purchase);
            return View(model);
        }

        /// <summary>
        /// Legacy single-item post (no Lines): the header fields become line 1, including the item / asset name
        /// (migration 108), so the name the user typed is not lost.
        /// </summary>
        public static void EnsureLegacyLine(PurchaseRequestCreateVm model)
        {
            if (model == null || (model.Lines != null && model.Lines.Count > 0))
            {
                return;
            }

            model.Lines = new List<PurchaseRequestLineCreateVm>
            {
                new PurchaseRequestLineCreateVm
                {
                    ItemName = model.ItemName,
                    Description = model.ItemDescription,
                    Quantity = model.Quantity > 0 ? model.Quantity : 1
                }
            };
        }

        /// <summary>
        /// Migration 108: tags are per line. An older link / asset request posting the header tag shows it on the first
        /// line that has a description (the service drops blank rows, so a tag parked on a blank row 0 would be lost);
        /// with no described line it goes to row 0 so the re-rendered form still shows it. When no line is tagged the
        /// header tag is left in place for the service, which applies it to the first effective line.
        /// </summary>
        public static void MoveHeaderTagToLine(PurchaseRequestCreateVm model)
        {
            if (model == null || model.Lines == null || model.Lines.Count == 0)
            {
                return;
            }

            if (!model.TargetAssetId.HasValue || model.TargetAssetId.Value <= 0)
            {
                model.TargetAssetId = null;
                return;
            }

            if (model.Lines.Any(x => x != null && x.TargetAssetId.HasValue && x.TargetAssetId.Value > 0))
            {
                model.TargetAssetId = null;
                return;
            }

            var target = model.Lines.FirstOrDefault(x => x != null && !string.IsNullOrWhiteSpace(x.Description))
                ?? model.Lines.FirstOrDefault(x => x != null);
            if (target == null)
            {
                return;
            }

            target.TargetAssetId = model.TargetAssetId;
            model.TargetAssetId = null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Create")]
        public ActionResult Create(PurchaseRequestCreateVm model, HttpPostedFileBase attachment, string returnUrl = null)
        {
            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId =>
            {
                if (deptId.HasValue)
                {
                    model.DepartmentId = deptId.Value;
                }
            });

            model.RequestForSelf = true;
            model.Currency = GetDefaultCurrencyCode();
            // "Order by" was removed from the requisition page (Allan, 2026-10-09); never accept a posted value.
            model.OrderByUserId = null;

            EnsureLegacyLine(model);

            PopulateCreateLookups(model);
            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            ViewBag.PurchaseApprovalSummary = BuildApprovalProcessSummary(ApprovalProcessCodes.Purchase);
            ApplyNoActiveSubTypeMessages(model);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                var submitted = _purchaseRequestService.SubmitBatch(model, User.GetUserId());
                var id = submitted.PrimaryId;
                // Split submits share one upload: every requisition of the batch points at the same stored file.
                SaveOptionalAttachment(id, attachment, submitted.RequestIds);
                if (submitted.IsSplit)
                {
                    TempData["Message"] = "Submitted " + submitted.RequestIds.Count + " requisitions ("
                        + string.Join(", ", submitted.RequestNumbers.ToArray()) + ", " + submitted.BatchLabel
                        + ") because the items use different approval paths.";
                    if (submitted.GroupId.HasValue)
                    {
                        return RedirectToAction("Batch", new { id = submitted.GroupId.Value, returnUrl = ViewBag.ReturnUrl });
                    }

                    return RedirectToAction("Details", new { id, returnUrl = ViewBag.ReturnUrl });
                }

                var created = _purchaseRequestService.GetById(id);
                var requestNumber = created == null || string.IsNullOrWhiteSpace(created.RequestNumber)
                    ? ("#" + id)
                    : created.RequestNumber;
                TempData["Message"] = "Submitted — awaiting approval. Requisition " + requestNumber + " is in the approval queue.";
                return RedirectToAction("Details", new { id, returnUrl = ViewBag.ReturnUrl });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        public ActionResult Details(int id, string returnUrl = null)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            EnrichUserNames(model);
            var currentRoleId = GetCurrentUserRoleId();
            var isSuperAdmin = IsCurrentUserSuperAdmin();
            var currentUserId = User.GetUserId();
            model.CanCurrentUserApprove = model.IsPending
                && ApprovalWorkflowHelper.CanUserActOnStage(
                    UnitOfWork,
                    model.RequestedById,
                    currentUserId,
                    isSuperAdmin,
                    currentRoleId,
                    model.CurrentStageRoleId,
                    model.CurrentStageUserId,
                    // rel1009e: same approver resolver as the notification and the approve/reject check.
                    StageApproverScopeResolver.ResolveFor(UnitOfWork, BuildUserService(), model.CurrentStageRoleId, model.CurrentStageUserId, model.DepartmentId, AssetManagement.Application.Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(model.ApprovalFlowSource)));

            // "Purchase order raised by" (resolved from the unit the flow is taken from): hide the PO button for others.
            var entity = UnitOfWork.Repository<Domain.Entities.PurchaseRequest>().GetById(id);
            var poRaiserConfig = PurchaseOrderRaiserRules.ResolveForRequest(UnitOfWork, entity);
            model.PurchaseOrderRaiserLabel = PurchaseOrderRaiserRules.Describe(UnitOfWork, BuildUserService(), poRaiserConfig);
            model.CanCurrentUserRaisePurchaseOrder = PurchaseOrderRaiserRules.CanRaise(
                poRaiserConfig,
                currentUserId,
                currentRoleId,
                PurchaseOrderRaiserRules.ResolveRaiserScope(UnitOfWork, BuildUserService(), poRaiserConfig, entity == null ? (int?)null : entity.DepartmentId, entity != null && AssetManagement.Application.Helpers.CatalogRequisitionFlowResolver.IsCatalogSource(entity.ApprovalFlowSource)));

            ViewBag.ReturnUrl = ResolveReturnUrl(returnUrl, "Index");
            // The path this requisition resolves to (its snapshot / unit flow), not the org default.
            ViewBag.PurchaseApprovalSummary = BuildPurchaseRequestApprovalSummary(entity);
            // rel1009i: every PO raised from this requisition, with ordered / received quantities.
            ViewBag.OrderSummary = BuildPurchaseService().GetRequisitionOrderSummary(id);
            return View(model);
        }

        [PermissionAuthorize("Purchases.View")]
        public ActionResult DownloadAttachment(int id)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null || !model.HasAttachment)
            {
                return HttpNotFound();
            }

            var relativePath = _purchaseRequestService.GetAttachmentRelativePath(id);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return HttpNotFound();
            }

            var content = _storage.OpenRead(relativePath);
            if (content == null)
            {
                return HttpNotFound();
            }

            var contentType = string.IsNullOrWhiteSpace(model.AttachmentContentType)
                ? "application/octet-stream"
                : model.AttachmentContentType;
            return File(content, contentType, model.AttachmentFileName);
        }

        [PermissionAuthorize("Purchases.View")]
        public ActionResult DownloadDocument(int id)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            EnrichUserNames(model);
            var generatedBy = User != null && User.Identity != null && User.Identity.IsAuthenticated
                ? User.Identity.Name
                : "System";
            var branding = ReportBrandingHelper.Resolve(
                UnitOfWork,
                DependencyResolver.Current.GetService<IOrganizationScopeService>(),
                ResolveApplicationBaseUrl());
            var html = ReportHtmlBuilder.BuildRequisitionDocument(model, generatedBy, branding);
            var fileName = SanitizeDownloadFileName(model.RequestNumber) + ".html";
            return File(Encoding.UTF8.GetBytes(html), "text/html; charset=utf-8", fileName);
        }

        [PermissionAuthorize("Purchases.View")]
        public JsonResult DocumentFragment(int id)
        {
            var model = _purchaseRequestService.GetById(id);
            if (model == null)
            {
                return Json(new { success = false, message = "Requisition not found." }, JsonRequestBehavior.AllowGet);
            }

            EnrichUserNames(model);
            var generatedBy = User != null && User.Identity != null && User.Identity.IsAuthenticated
                ? User.Identity.Name
                : "System";
            var branding = ReportBrandingHelper.Resolve(
                UnitOfWork,
                DependencyResolver.Current.GetService<IOrganizationScopeService>(),
                ResolveApplicationBaseUrl());
            var html = ReportHtmlBuilder.BuildRequisitionFragment(model, generatedBy, branding);
            var fileName = SanitizeDownloadFileName(model.RequestNumber) + ".pdf";
            return Json(new { success = true, html, fileName }, JsonRequestBehavior.AllowGet);
        }

        private static string SanitizeDownloadFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "requisition";
            }

            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(value.Where(ch => !invalid.Contains(ch)).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? "requisition" : cleaned;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Approve")]
        public ActionResult Approve(int id, string notes, string returnUrl = null)
        {
            try
            {
                _purchaseRequestService.Approve(new PurchaseRequestApprovalVm
                {
                    PurchaseRequestId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Requisition approval recorded.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id, returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Purchases.Approve")]
        public ActionResult Reject(int id, string notes, string returnUrl = null)
        {
            try
            {
                _purchaseRequestService.Reject(new PurchaseRequestApprovalVm
                {
                    PurchaseRequestId = id,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Requisition rejected.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id, returnUrl });
        }

        [PermissionAuthorize("Purchases.Create")]
        public JsonResult PreviewApprovalPath(int? departmentId = null)
        {
            return Json(new
            {
                summary = BuildDepartmentRequisitionApprovalSummary(departmentId)
            }, JsonRequestBehavior.AllowGet);
        }

        [PermissionAuthorize("Purchases.Create")]
        public JsonResult SearchTargetAssets(string search = null, int? departmentId = null, AssetStatus? status = null, string sort = "tag", string direction = "asc", int page = 1, int pageSize = 10)
        {
            var filter = new AssetFilterVm
            {
                Search = search,
                DepartmentId = departmentId,
                Status = status,
                OrganizationWide = true
            };

            var pageModel = BuildAssetService().GetAssetListPage(filter, sort, direction, page, pageSize);
            EnrichAssetListCustodianNames(pageModel.Items);
            var listPage = ToAssetListPage(pageModel);
            var inStockLookup = BuildInStockQuantityLookup(pageModel.Items);

            var items = pageModel.Items.Select(x => new
            {
                id = x.Id,
                assetTag = x.AssetTag,
                assetName = x.AssetName,
                categoryName = x.CategoryName,
                departmentId = x.DepartmentId,
                departmentName = string.IsNullOrWhiteSpace(x.DepartmentName) ? "Company custody" : x.DepartmentName,
                custodianName = string.IsNullOrWhiteSpace(x.CurrentCustodianName) ? "" : x.CurrentCustodianName,
                status = FormatAssetStatusLabel(x.CurrentStatus),
                statusBadge = StatusHtmlHelpers.ToBadgeClass(x.CurrentStatus),
                acquisitionCost = x.AcquisitionCost,
                acquisitionCostDisplay = AssetPriceRules.Format(x.AcquisitionCost),
                label = FormatTargetAssetLabel(x),
                itemDescription = FormatTargetAssetItemDescription(x),
                assetTypeName = string.IsNullOrWhiteSpace(x.AssetTypeName) ? "" : x.AssetTypeName,
                subTypeName = string.IsNullOrWhiteSpace(x.AssetSubTypeName) ? "" : x.AssetSubTypeName,
                quantityInStock = ResolveInStockQuantity(x, inStockLookup)
            }).ToList();

            return Json(new
            {
                items = items,
                page = listPage.Page,
                pageSize = listPage.PageSize,
                totalCount = listPage.TotalCount,
                totalPages = listPage.TotalPages,
                startItem = listPage.StartItem,
                endItem = listPage.EndItem,
                sort = sort,
                direction = direction
            }, JsonRequestBehavior.AllowGet);
        }

        [PermissionAuthorize("Purchases.Create")]
        public JsonResult AvailableTargetAssets()
        {
            var items = GetTargetAssetOptions()
                .Select(x => new { id = x.Value, name = x.Text })
                .ToList();
            return Json(items, JsonRequestBehavior.AllowGet);
        }

        private void PopulateCreateLookups(PurchaseRequestCreateVm model)
        {
            var canCreateForAnyDepartment = HasPermission("Purchases.CreateForAnyDepartment");
            var lockDepartment = !canCreateForAnyDepartment
                && !IsCurrentUserSuperAdmin()
                && GetCurrentUserDepartmentId().HasValue;
            int? departmentId = model == null ? GetCurrentUserDepartmentId() : (int?)model.DepartmentId;
            ViewBag.CanCreateForAnyDepartment = canCreateForAnyDepartment;
            ViewBag.LockDepartment = lockDepartment;
            ViewBag.DepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                departmentId,
                GetActiveDepartments());
            var departmentGroups = canCreateForAnyDepartment
                ? BuildRequisitionDepartmentGroups(departmentId)
                : null;
            ViewBag.DepartmentGroups = departmentGroups;
            // Never leave the DepartmentId dropdown without a list: with no groups (e.g. a department scope that
            // returned nothing) the view used to fall through to DropDownListFor with a null SelectList, which
            // throws "There is no ViewData item of type IEnumerable<SelectListItem> that has the key 'DepartmentId'".
            ViewBag.Departments = departmentGroups != null && departmentGroups.Count > 0
                ? null
                : BuildDepartmentSelectList(departmentId);
            ViewBag.TargetAssetSearchUrl = TenantUrlHelper.TenantRouteUrl(Url, "SearchTargetAssets", "PurchaseRequests");
            ViewBag.PreviewApprovalPathUrl = TenantUrlHelper.TenantRouteUrl(Url, "PreviewApprovalPath", "PurchaseRequests");
            PopulateCatalogPickers();
            // Migration 108: tags are per line. An older link / asset request posting the header tag shows it on line 1.
            if (model != null)
            {
                EnsureLegacyLine(model);
                MoveHeaderTagToLine(model);
            }

            var lineTargetLabels = new Dictionary<int, string>();
            var lineTargetClassifications = new Dictionary<int, string[]>();
            if (model != null)
            {
                for (var i = 0; i < model.Lines.Count; i++)
                {
                    var line = model.Lines[i];
                    if (line != null && line.TargetAssetId.HasValue && line.TargetAssetId.Value > 0)
                    {
                        var asset = ResolveSelectedTargetAsset(line.TargetAssetId);
                        lineTargetLabels[i] = asset == null ? string.Empty : FormatTargetAssetLabel(asset);
                        lineTargetClassifications[i] = FormatTargetAssetClassification(asset);
                    }
                }
            }

            ViewBag.LineTargetAssetLabels = lineTargetLabels;
            ViewBag.LineTargetClassifications = lineTargetClassifications;
            ViewBag.SelectedTargetAssetLabel = string.Empty;
            var profile = GetCurrentUserProfile();
            ViewBag.RequestingAsDisplayName = profile == null
                ? (User != null && User.Identity != null ? User.Identity.Name : "Current user")
                : BuildUserLabel(profile);
        }

        private AssetListVm ResolveSelectedTargetAsset(int? assetId)
        {
            if (!assetId.HasValue || assetId.Value <= 0)
            {
                return null;
            }

            var page = BuildAssetService().GetAssetListPage(
                new AssetFilterVm { OrganizationWide = true },
                "tag",
                "asc",
                1,
                500);
            return page.Items.FirstOrDefault(x => x.Id == assetId.Value);
        }

        /// <summary>QA 2026-10-28: [Category, Type, Sub-type] of a tagged line's asset, shown read-only on the form.</summary>
        public static string[] FormatTargetAssetClassification(AssetListVm asset)
        {
            if (asset == null)
            {
                return new[] { string.Empty, string.Empty, string.Empty };
            }

            return new[]
            {
                asset.CategoryName ?? string.Empty,
                asset.AssetTypeName ?? string.Empty,
                asset.AssetSubTypeName ?? string.Empty
            };
        }

        private static string FormatAssetStatusLabel(AssetStatus status)
        {
            return status.ToString()
                .Replace("AwaitingApproval", "Pending Approval")
                .Replace("InStore", "In Store");
        }

        private IList<SelectListItem> GetTargetAssetOptions()
        {
            var page = BuildAssetService().GetAssetListPage(
                new AssetFilterVm { OrganizationWide = true },
                "tag",
                "asc",
                1,
                500);

            return page.Items
                .OrderBy(x => x.AssetTag)
                .ThenBy(x => x.AssetName)
                .Select(x => new SelectListItem
                {
                    Value = x.Id.ToString(),
                    Text = FormatTargetAssetLabel(x)
                })
                .ToList();
        }

        private static string FormatTargetAssetLabel(AssetListVm asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            var label = string.IsNullOrWhiteSpace(asset.AssetTag)
                ? asset.AssetName
                : asset.AssetTag + " - " + asset.AssetName;
            if (!string.IsNullOrWhiteSpace(asset.DepartmentName))
            {
                label += " · " + asset.DepartmentName;
            }

            return label;
        }

        private static string FormatTargetAssetItemDescription(AssetListVm asset)
        {
            if (asset == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(asset.AssetTag) && !string.IsNullOrWhiteSpace(asset.AssetName))
            {
                return asset.AssetTag + " - " + asset.AssetName;
            }

            if (!string.IsNullOrWhiteSpace(asset.AssetName))
            {
                return asset.AssetName;
            }

            return asset.AssetTag ?? string.Empty;
        }

        private Dictionary<string, int> BuildInStockQuantityLookup(IList<AssetListVm> items)
        {
            var lookup = new Dictionary<string, int>(StringComparer.Ordinal);
            if (items == null || items.Count == 0)
            {
                return lookup;
            }

            var stockService = BuildAssetStockService();
            foreach (var group in items.GroupBy(BuildInStockQuantityKey))
            {
                if (lookup.ContainsKey(group.Key))
                {
                    continue;
                }

                var sample = group.First();
                if (sample.AssetSubTypeId.HasValue && sample.AssetSubTypeId.Value > 0)
                {
                    lookup[group.Key] = stockService.GetAvailableQuantity(sample.AssetSubTypeId.Value, sample.DepartmentId);
                    continue;
                }

                if (sample.AssetTypeId <= 0)
                {
                    lookup[group.Key] = sample.CurrentStatus == AssetStatus.InStore ? 1 : 0;
                    continue;
                }

                lookup[group.Key] = BuildAssetService().CountAssets(new AssetFilterVm
                {
                    OrganizationWide = true,
                    AssetTypeId = sample.AssetTypeId,
                    DepartmentId = sample.DepartmentId,
                    Status = AssetStatus.InStore
                });
            }

            return lookup;
        }

        private static int ResolveInStockQuantity(AssetListVm asset, IDictionary<string, int> lookup)
        {
            if (asset == null || lookup == null)
            {
                return 0;
            }

            int quantity;
            return lookup.TryGetValue(BuildInStockQuantityKey(asset), out quantity) ? quantity : 0;
        }

        private static string BuildInStockQuantityKey(AssetListVm asset)
        {
            var departmentId = asset.DepartmentId.GetValueOrDefault(0);
            if (asset.AssetSubTypeId.HasValue && asset.AssetSubTypeId.Value > 0)
            {
                return "sub:" + departmentId + ":" + asset.AssetSubTypeId.Value;
            }

            return "type:" + departmentId + ":" + asset.AssetTypeId;
        }

        private void EnrichUserNames(PurchaseRequestDetailVm model)
        {
            var requester = BuildUserService().GetById(model.RequestedById);
            model.RequestedByName = requester == null ? model.RequestedById : BuildUserLabel(requester);
        }

        private void SaveOptionalAttachment(int purchaseRequestId, HttpPostedFileBase attachment, IList<int> batchRequestIds = null)
        {
            if (attachment == null || attachment.ContentLength <= 0 || _storage == null)
            {
                return;
            }

            var extension = Path.GetExtension(attachment.FileName);
            if (string.IsNullOrWhiteSpace(extension)
                || !AllowedAttachmentExtensions.Contains(extension.ToLowerInvariant())
                || attachment.ContentLength > MaxAttachmentSizeBytes)
            {
                TempData["Error"] = "Attachment was skipped because the file type or size is not allowed.";
                return;
            }

            var storedFileName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
            using (var stream = attachment.InputStream)
            {
                var relativePath = _storage.Save(
                    stream,
                    storedFileName,
                    attachment.ContentType,
                    "purchase-requests/" + purchaseRequestId);
                var info = new PurchaseRequestAttachmentInfo
                {
                    FileName = Path.GetFileName(attachment.FileName),
                    FilePath = relativePath,
                    ContentType = attachment.ContentType,
                    FileSizeBytes = attachment.ContentLength
                };
                _purchaseRequestService.SaveAttachment(purchaseRequestId, info);
                foreach (var siblingId in (batchRequestIds ?? new List<int>()).Where(x => x != purchaseRequestId))
                {
                    _purchaseRequestService.SaveAttachment(siblingId, info);
                }
            }
        }
    }
}
