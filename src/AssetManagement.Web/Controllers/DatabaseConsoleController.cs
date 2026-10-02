using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Security;
using AssetManagement.Web.Services;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Hidden Platform Admin SQL console — not linked from navigation.
    /// Access via /Sys/Database, /Sys/DataConsole, or /DatabaseConsole/Index.
    /// Unauthenticated → Login (via [Authorize]). Tenant/company Admin → Forbidden.
    /// Only IOrganizationScopeService.IsActualPlatformAdmin() may use the console.
    /// </summary>
    [Authorize]
    public class DatabaseConsoleController : Controller
    {
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;
        private readonly DatabaseConsoleService _consoleService;

        public DatabaseConsoleController()
        {
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
            _consoleService = DependencyResolver.Current.GetService<DatabaseConsoleService>();
        }

        public ActionResult Index()
        {
            var gate = GateConsoleAccess();
            if (gate != null)
            {
                return gate;
            }

            var model = new DatabaseConsoleViewModel();
            _consoleService.LoadSchema(model);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Execute(string sql)
        {
            var gate = GateConsoleAccess();
            if (gate != null)
            {
                return gate;
            }

            var model = _consoleService.Execute(sql);
            _consoleService.LoadSchema(model);

            TryAuditExecute(sql, model);

            return View("Index", model);
        }

        /// <summary>
        /// Explicit gate so company/tenant Admin never sees the console or an ambiguous soft-404.
        /// </summary>
        private ActionResult GateConsoleAccess()
        {
            if (User == null || User.Identity == null || !User.Identity.IsAuthenticated)
            {
                var returnUrl = Request != null ? Request.RawUrl : "/Sys/Database";
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrl });
            }

            if (_organizationScope == null || !_organizationScope.IsActualPlatformAdmin())
            {
                TempData["ErrorMessage"] = "The database console is restricted to Platform Admin (SuperAdmin).";
                return RedirectToAction("Forbidden", "Home");
            }

            return null;
        }

        private void TryAuditExecute(string sql, DatabaseConsoleViewModel model)
        {
            if (_auditWriter == null || model == null)
            {
                return;
            }

            try
            {
                var userId = User.GetUserId();
                _auditWriter.Write(
                    "DB_CONSOLE_EXECUTE",
                    "ApplicationUser",
                    userId,
                    TruncateForAudit(sql),
                    model.IsError ? "error" : "ok");
            }
            catch
            {
                // Audit must not block the console; platform admins may lack org context.
            }
        }

        private static string TruncateForAudit(string sql)
        {
            if (string.IsNullOrEmpty(sql))
            {
                return null;
            }

            const int maxLength = 1000;
            var trimmed = sql.Trim();
            return trimmed.Length <= maxLength
                ? trimmed
                : trimmed.Substring(0, maxLength) + "...";
        }
    }
}
