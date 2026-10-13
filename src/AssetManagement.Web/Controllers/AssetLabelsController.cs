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

        public AssetLabelsController()
            : this(DependencyResolver.Current.GetService<IDepartmentService>())
        {
        }

        public AssetLabelsController(IDepartmentService departmentService)
        {
            _departmentService = departmentService;
        }

        [HttpGet]
        [Authorize]
        [TenantAuthorize]
        [PermissionAuthorize("Assets.View")]
        public ActionResult BulkPrint()
        {
            var model = new AssetScanLookupPageVm
            {
                Lookup = new AssetScanLookupVm { Found = false },
                IsPublicScan = false,
                CanBatchPrintLabels = true,
                PrintCandidatesUrl = TenantUrlHelper.TenantRouteUrl(Url, "PrintCandidates", "AssetScan"),
                LabelPrintConfigUrl = TenantUrlHelper.TenantRouteUrl(Url, "LabelPrintConfig", "Assets", new { id = "__id__" }),
                LabelZplUrlTemplate = TenantUrlHelper.TenantRouteUrl(Url, "LabelZpl", "Assets", new { id = "__id__" }),
                DepartmentOptions = AssetScanController.BuildDepartmentOptions(_departmentService),
                StatusOptions = AssetScanController.BuildStatusOptions()
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
