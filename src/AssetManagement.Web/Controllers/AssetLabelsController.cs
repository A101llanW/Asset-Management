using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
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
        public ActionResult BulkPrint(int? purchaseId = null)
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
                InitialPurchaseLabel = initialPurchase == null ? null : PurchaseFilterOptions.Label(initialPurchase)
            };

            return View(model);
        }

        [HttpGet]
        public ActionResult Index()
        {
            return RedirectToAction("BulkPrint");
        }
    }
}
