using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Areas.Platform.Controllers
{
    [PermissionAuthorize("Platform.Organizations.View")]
    public class SecurityLogsController : Controller
    {
        private readonly ISecurityLogService _securityLogService;
        private readonly ISecurityReportExportService _exportService;
        private readonly IAccountSecurityService _accountSecurityService;
        private readonly IAuditWriter _auditWriter;

        public SecurityLogsController()
        {
            _securityLogService = DependencyResolver.Current.GetService<ISecurityLogService>();
            _exportService = DependencyResolver.Current.GetService<ISecurityReportExportService>();
            _accountSecurityService = DependencyResolver.Current.GetService<IAccountSecurityService>();
            _auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
        }
        public ActionResult Index(SecurityLogFilterVm filter, string tab)
        {
            var model = _securityLogService == null
                ? new SecurityLogsPageVm { Filter = filter ?? new SecurityLogFilterVm() }
                : _securityLogService.GetLogs(filter ?? new SecurityLogFilterVm(), true);
            ViewBag.ActiveTab = string.IsNullOrWhiteSpace(tab) ? "login" : tab;
            return View(model);
        }

        public ActionResult ExportCsv(SecurityLogFilterVm filter)
        {
            var page = _securityLogService == null
                ? new SecurityLogsPageVm { Filter = filter ?? new SecurityLogFilterVm() }
                : _securityLogService.GetLogs(filter ?? new SecurityLogFilterVm(), true);
            var bytes = _exportService == null ? new byte[0] : _exportService.ExportCsv(page);
            return File(bytes, "text/csv", "platform-security-report.csv");
        }

        public ActionResult ExportHtml(SecurityLogFilterVm filter)
        {
            var page = _securityLogService == null
                ? new SecurityLogsPageVm { Filter = filter ?? new SecurityLogFilterVm() }
                : _securityLogService.GetLogs(filter ?? new SecurityLogFilterVm(), true);
            var baseUrl = Request != null && Request.Url != null ? Request.Url.GetLeftPart(System.UriPartial.Authority) : null;
            var html = _exportService == null ? "<html><body>No data</body></html>" : _exportService.ExportHtml(page, baseUrl);
            return Content(html, "text/html");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Platform.Organizations.Manage")]
        public ActionResult InvalidateAllSessions()
        {
            if (_accountSecurityService == null)
            {
                TempData["Error"] = "Account security service is unavailable.";
                return RedirectToAction("Index");
            }

            var invalidatedCount = _accountSecurityService.InvalidateAllActiveSessions(null);
            _auditWriter?.Write(
                "Security.InvalidateAllSessions",
                "ApplicationUser",
                "ALL",
                null,
                "count=" + invalidatedCount + ";scope=platform");

            TempData["Message"] = invalidatedCount + " active user session(s) invalidated across all organizations. Users will be signed out on their next request.";
            return RedirectToAction("Index");
        }
    }
}
