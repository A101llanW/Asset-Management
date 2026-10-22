using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Asset label tools that live outside the Scan &amp; Search page.
    /// Bulk label print used to be a panel on AssetScan/Lookup; it now has its own page under Assets &amp; custody.
    /// Access is unchanged: any signed-in tenant user with Assets.View (the same check the old panel and
    /// AssetScan/PrintCandidates use).
    /// </summary>
    [RequireTenantRoute]
    public class AssetLabelsController : Controller
    {
        private readonly IDepartmentService _departmentService;
        private readonly IPurchaseService _purchaseService;

        public AssetLabelsController()
            : this(
                DependencyResolver.Current.GetService<IDepartmentService>(),
                DependencyResolver.Current.GetService<IPurchaseService>())
        {
        }

        public AssetLabelsController(IDepartmentService departmentService)
            : this(departmentService, null)
        {
        }

        public AssetLabelsController(IDepartmentService departmentService, IPurchaseService purchaseService)
        {
            _departmentService = departmentService;
            _purchaseService = purchaseService;
        }

        [HttpGet]
        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public ActionResult BulkPrint(int? purchaseId = null, int? batchId = null)
        {
            // rel1014a: ?purchaseId= comes from "Send to Bulk Label Print" on a purchase receipt.
            // Only the tenant's own purchases are offered or honoured.
            var purchases = _purchaseService == null
                ? new System.Collections.Generic.List<PurchaseRecordVm>()
                : (_purchaseService.GetAll() ?? new System.Collections.Generic.List<PurchaseRecordVm>()).ToList();
            var initialPurchaseId = PurchaseFilterOptions.ResolveTenantPurchaseId(purchases, purchaseId);
            var initialPurchase = initialPurchaseId.HasValue ? purchases.FirstOrDefault(p => p.Id == initialPurchaseId.Value) : null;

            var model = new AssetScanLookupPageVm
            {
                Lookup = new AssetScanLookupVm { Found = false },
                IsPublicScan = false,
                CanBatchPrintLabels = true,
                PrintCandidatesUrl = TenantUrlHelper.TenantRouteUrl(Url, "PrintCandidates", "AssetScan"),
                LabelPrintConfigUrl = TenantUrlHelper.TenantRouteUrl(Url, "LabelPrintConfig", "Assets", new { id = "__id__" }),
                LabelZplUrlTemplate = TenantUrlHelper.TenantRouteUrl(Url, "LabelZpl", "Assets", new { id = "__id__" }),
                DepartmentOptions = AssetScanController.BuildDepartmentOptions(_departmentService),
                StatusOptions = AssetScanController.BuildStatusOptions(),
                PurchaseOptions = PurchaseFilterOptions.Build(purchases, initialPurchaseId),
                InitialPurchaseId = initialPurchaseId,
                InitialPurchaseLabel = initialPurchase == null ? null : PurchaseFilterOptions.Label(initialPurchase),
                // Saved Label designer templates (read-only list; Assets.View is enough to print with one).
                LabelTemplateOptions = LabelPrintSupport.BuildTemplateOptions(DependencyResolver.Current.GetService<IUnitOfWork>()),
                // Category > type > sub-type cascade for the candidate filters (this org, active only).
                ClassificationCatalog = LabelPrintSupport.BuildClassificationCatalog(DependencyResolver.Current.GetService<IUnitOfWork>())
            };

            // rel1022a: print log endpoints (record each printed label, batches, the collapsed batch log).
            model.PrintLog = BuildPrintLogUrls();
            if (batchId.HasValue && batchId.Value > 0)
            {
                // "Send to printing" from the Asset register: preload that batch into the print queue.
                var service = PrintLogService;
                var batch = service == null ? null : service.GetBatch(batchId.Value, User.GetUserId(), CanManageAllBatches());
                if (batch != null && !batch.IsCleared && batch.Items.Count > 0)
                {
                    model.InitialPrintBatch = batch;
                }
                else
                {
                    model.InitialPrintBatchMissing = true;
                }
            }

            return View(model);
        }

        /// <summary>
        /// The browser has printed one or more labels (Zebra Browser Print) or opened the print dialog for one.
        /// Writes one audit row per asset (Assets.LabelPrint or Assets.LabelReprint). Nothing is sent to a printer here.
        /// </summary>
        [HttpPost]
        [Authorize]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.View")]
        public JsonResult RecordPrint(int[] assetIds, int? batchId = null, string source = null, string mode = null, string templateId = null, string codeType = null, bool reprint = false)
        {
            return RunJson(service =>
            {
                var result = service.RecordPrints(new LabelPrintRecordRequestVm
                {
                    AssetIds = assetIds == null ? new List<int>() : assetIds.ToList(),
                    BatchId = batchId,
                    Source = source,
                    Mode = mode,
                    TemplateId = templateId,
                    CodeType = codeType,
                    Reprint = reprint
                }, User.GetUserId());
                return new
                {
                    success = true,
                    recorded = result.Items.Select(x => new { assetId = x.AssetId, assetTag = x.AssetTag, isReprint = x.IsReprint }).ToList(),
                    skipped = result.SkippedCount
                };
            });
        }

        /// <summary>Starts a print batch for the current queue (Bulk Label Print), or a reprint of an earlier batch.</summary>
        [HttpPost]
        [Authorize]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.View")]
        public JsonResult CreateBatch(int[] assetIds, string groupLabel = null, string filterDescription = null, int? reprintOfBatchId = null, string templateId = null, string codeType = null)
        {
            return RunJson(service =>
            {
                var batch = service.CreateBatch(new LabelPrintBatchCreateVm
                {
                    AssetIds = assetIds == null ? new List<int>() : assetIds.ToList(),
                    Source = LabelPrintLogService.SourceBulkPrint,
                    GroupLabel = groupLabel,
                    FilterDescription = filterDescription,
                    ReprintOfBatchId = reprintOfBatchId,
                    TemplateId = templateId,
                    CodeType = codeType
                }, User.GetUserId());
                return new { success = true, batch = ToJson(batch, true) };
            });
        }

        [HttpPost]
        [Authorize]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.View")]
        public JsonResult CompleteBatch(int batchId, bool stoppedEarly = false)
        {
            return RunJson(service =>
            {
                service.CompleteBatch(batchId, stoppedEarly, User.GetUserId());
                return new { success = true };
            });
        }

        /// <summary>The collapsed "Print batch log" drawer: latest batches of this organization (cleared ones only on request).</summary>
        [HttpGet]
        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public JsonResult Batches(bool includeCleared = false, int take = 25)
        {
            return RunJson(service =>
            {
                var batches = service.GetBatches(includeCleared, take, User.GetUserId(), CanManageAllBatches());
                return new { success = true, batches = batches.Select(b => ToJson(b, false)).ToList() };
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Asset list of one batch (for "Reprint group").</summary>
        [HttpGet]
        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public JsonResult BatchItems(int id)
        {
            return RunJson(service =>
            {
                var batch = service.GetBatch(id, User.GetUserId(), CanManageAllBatches());
                if (batch == null)
                {
                    throw new BusinessException("The print batch was not found.");
                }

                return new { success = true, batch = ToJson(batch, true) };
            }, JsonRequestBehavior.AllowGet);
        }

        /// <summary>Soft-hides one batch entry (Cleared flag, who, when). The per-asset print audit trail is kept.</summary>
        [HttpPost]
        [Authorize]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.View")]
        public JsonResult ClearBatch(int id)
        {
            return RunJson(service =>
            {
                var cleared = service.ClearBatch(id, User.GetUserId(), CanManageAllBatches());
                return new { success = true, cleared = cleared };
            });
        }

        /// <summary>
        /// Asset register "Send to printing": the selected rows / whole groups become ONE print batch, then the
        /// Bulk Label Print page opens with that batch in its queue. Printing still starts only when the user prints.
        /// </summary>
        [HttpPost]
        [Authorize]
        [TenantAuthorize]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.View")]
        public ActionResult QueueFromRegister(int[] assetIds, string groupLabel = null, string filterDescription = null, string returnUrl = null)
        {
            var service = PrintLogService;
            if (service == null)
            {
                TempData["Error"] = "Label printing is not available.";
                return RedirectToRegister(returnUrl);
            }

            try
            {
                var batch = service.CreateBatch(new LabelPrintBatchCreateVm
                {
                    AssetIds = assetIds == null ? new List<int>() : assetIds.ToList(),
                    Source = LabelPrintLogService.SourceAssetRegister,
                    GroupLabel = groupLabel,
                    FilterDescription = filterDescription
                }, User.GetUserId());
                TempData["Message"] = "Batch #" + batch.Id + " with " + batch.Items.Count + " label(s) is in the print queue. Check the template, then print.";
                return Redirect(TenantUrlHelper.TenantRouteUrl(Url, "BulkPrint", "AssetLabels", new { batchId = batch.Id }));
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToRegister(returnUrl);
            }
        }

        private ActionResult RedirectToRegister(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return Redirect(TenantUrlHelper.TenantRouteUrl(Url, "Index", "Assets"));
        }

        private LabelPrintLogUrlsVm BuildPrintLogUrls()
        {
            return new LabelPrintLogUrlsVm
            {
                RecordPrintUrl = TenantUrlHelper.TenantRouteUrl(Url, "RecordPrint", "AssetLabels"),
                CreateBatchUrl = TenantUrlHelper.TenantRouteUrl(Url, "CreateBatch", "AssetLabels"),
                CompleteBatchUrl = TenantUrlHelper.TenantRouteUrl(Url, "CompleteBatch", "AssetLabels"),
                BatchesUrl = TenantUrlHelper.TenantRouteUrl(Url, "Batches", "AssetLabels"),
                BatchItemsUrl = TenantUrlHelper.TenantRouteUrl(Url, "BatchItems", "AssetLabels"),
                ClearBatchUrl = TenantUrlHelper.TenantRouteUrl(Url, "ClearBatch", "AssetLabels")
            };
        }

        public static object ToJson(LabelPrintBatchVm b, bool includeItems)
        {
            return new
            {
                id = b.Id,
                source = b.Source,
                sourceLabel = b.SourceLabel,
                groupLabel = b.GroupLabel,
                filterDescription = b.FilterDescription,
                assetCount = b.AssetCount,
                printedCount = b.PrintedCount,
                status = b.Status,
                isReprint = b.IsReprint,
                reprintOfBatchId = b.ReprintOfBatchId,
                createdByName = b.CreatedByName,
                createdAt = b.CreatedAtDisplay,
                lastPrintedAt = b.LastPrintedAtDisplay,
                isCleared = b.IsCleared,
                clearedByName = b.ClearedByName,
                clearedAt = b.ClearedAtDisplay,
                canClear = b.CanClear,
                items = includeItems
                    ? b.Items.Select(i => new { id = i.AssetId, tag = i.AssetTag, name = i.AssetName, printCount = i.PrintCount }).ToList()
                    : null
            };
        }

        private JsonResult RunJson(Func<ILabelPrintLogService, object> action, JsonRequestBehavior behavior = JsonRequestBehavior.DenyGet)
        {
            var service = PrintLogService;
            if (service == null)
            {
                return Json(new { success = false, message = "Label print logging is not available." }, behavior);
            }

            try
            {
                return Json(action(service), behavior);
            }
            catch (BusinessException ex)
            {
                return Json(new { success = false, message = ex.Message }, behavior);
            }
        }

        private ILabelPrintLogService PrintLogService
        {
            get { return DependencyResolver.Current.GetService<ILabelPrintLogService>(); }
        }

        /// <summary>Users who can edit assets may clear anyone's batch entry; others only their own.</summary>
        private bool CanManageAllBatches()
        {
            var authorization = DependencyResolver.Current.GetService<IAuthorizationService>();
            return authorization != null && authorization.HasPermission(User.GetUserId(), "Assets.Edit");
        }

        [HttpGet]
        public ActionResult Index()
        {
            return RedirectToAction("BulkPrint");
        }
    }
}
