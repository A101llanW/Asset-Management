using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("AuditLogs.View")]
    public class AuditLogsController : BaseController
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController()
        {
            _auditLogService = BuildAuditLogService();
        }

        public ActionResult Index(AuditLogFilterVm filter)
        {
            filter = filter ?? new AuditLogFilterVm();
            if (filter.RelatedAssetId.HasValue)
            {
                var assetDetails = BuildAssetService().GetById(filter.RelatedAssetId.Value);
                if (assetDetails == null)
                {
                    return HttpNotFound();
                }

                ViewBag.RelatedAssetTag = assetDetails.AssetTag;
                ViewBag.RelatedAssetName = assetDetails.AssetName;
                ViewBag.BackUrl = Url.Action("Details", "Assets", new { id = assetDetails.Id });
            }

            ViewBag.Filter = filter;
            return View(_auditLogService.GetLogs(filter));
        }

        [PermissionAuthorize("AuditLogs.Export")]
        public ActionResult Export(AuditLogFilterVm filter)
        {
            filter = filter ?? new AuditLogFilterVm();
            return File(
                _auditLogService.ExportCsv(filter),
                "text/csv",
                "audit-log-" + System.DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }
    }
}
