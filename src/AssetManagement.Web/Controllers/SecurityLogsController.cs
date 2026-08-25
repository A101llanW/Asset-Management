using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.Security;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("SecurityLogs.View")]
    [ModuleAccess(ModulePermissionCatalog.SecurityLogs)]
    public class SecurityLogsController : BaseController
    {
        private readonly ISecurityLogService _securityLogService;
        private readonly ISecurityReportExportService _exportService;
        private readonly IAccountSecurityService _accountSecurityService;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;

        public SecurityLogsController()
        {
            _securityLogService = DependencyResolver.Current.GetService<ISecurityLogService>();
            _exportService = DependencyResolver.Current.GetService<ISecurityReportExportService>();
            _accountSecurityService = DependencyResolver.Current.GetService<IAccountSecurityService>();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
        }

        public ActionResult Index(SecurityLogFilterVm filter, string tab)
        {
            var model = _securityLogService == null
                ? new SecurityLogsPageVm { Filter = filter ?? new SecurityLogFilterVm() }
                : _securityLogService.GetLogs(filter ?? new SecurityLogFilterVm(), false);
            ViewBag.ActiveTab = string.IsNullOrWhiteSpace(tab) ? "login" : tab;
            return View(model);
        }

        public ActionResult ExportCsv(SecurityLogFilterVm filter)
        {
            var page = _securityLogService == null
                ? new SecurityLogsPageVm { Filter = filter ?? new SecurityLogFilterVm() }
                : _securityLogService.GetLogs(filter ?? new SecurityLogFilterVm(), false);
            var bytes = _exportService == null ? new byte[0] : _exportService.ExportCsv(page);
            return File(bytes, "text/csv", "security-report.csv");
        }

        public ActionResult ExportHtml(SecurityLogFilterVm filter)
        {
            var page = _securityLogService == null
                ? new SecurityLogsPageVm { Filter = filter ?? new SecurityLogFilterVm() }
                : _securityLogService.GetLogs(filter ?? new SecurityLogFilterVm(), false);
            var html = _exportService == null ? "<html><body>No data</body></html>" : _exportService.ExportHtml(page, ResolveApplicationBaseUrl());
            return Content(html, "text/html");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Settings.Manage")]
        public ActionResult InvalidateAllSessions()
        {
            if (_accountSecurityService == null)
            {
                TempData["Error"] = "Account security service is unavailable.";
                return RedirectToAction("Index");
            }

            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                TempData["Error"] = "Organization context is required to sign out tenant users.";
                return RedirectToAction("Index");
            }

            var invalidatedCount = _accountSecurityService.InvalidateAllActiveSessions(organizationId);
            _auditWriter?.Write(
                "Security.InvalidateAllSessions",
                "ApplicationUser",
                "ALL",
                null,
                "count=" + invalidatedCount + ";organizationId=" + organizationId.Value);

            TempData["Message"] = invalidatedCount + " active user session(s) invalidated. Users will be signed out on their next request.";
            return RedirectToAction("Index");
        }
    }
}
