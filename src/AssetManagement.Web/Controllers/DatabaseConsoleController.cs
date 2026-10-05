using System;
using System.Configuration;
using System.Linq;
using System.Net;
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
    /// Phase B (#3): IP allowlist via DatabaseConsoleAllowedIps + richer audit.
    /// </summary>
    [Authorize]
    public class DatabaseConsoleController : Controller
    {
        private const int AuditSqlMaxLength = 8000;
        private const string AllowedIpsAppSettingKey = "DatabaseConsoleAllowedIps";

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
        /// Phase B: also enforce DatabaseConsoleAllowedIps (default loopback).
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

            if (!IsClientIpAllowed())
            {
                TempData["ErrorMessage"] = "The database console is restricted by IP allowlist.";
                return RedirectToAction("Forbidden", "Home");
            }

            return null;
        }

        private bool IsClientIpAllowed()
        {
            var raw = ConfigurationManager.AppSettings[AllowedIpsAppSettingKey];
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = "127.0.0.1,::1";
            }

            var allowed = raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToList();

            if (allowed.Count == 0)
            {
                allowed.Add("127.0.0.1");
                allowed.Add("::1");
            }

            var clientIp = ResolveClientIp();
            if (string.IsNullOrWhiteSpace(clientIp))
            {
                return false;
            }

            // Normalize IPv6 loopback / IPv4-mapped forms for comparison.
            IPAddress parsedClient;
            if (!IPAddress.TryParse(clientIp, out parsedClient))
            {
                return allowed.Any(a => string.Equals(a, clientIp, StringComparison.OrdinalIgnoreCase));
            }

            if (IPAddress.IsLoopback(parsedClient))
            {
                return allowed.Any(a =>
                {
                    IPAddress parsedAllowed;
                    return string.Equals(a, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(a, "::1", StringComparison.OrdinalIgnoreCase)
                        || (IPAddress.TryParse(a, out parsedAllowed) && IPAddress.IsLoopback(parsedAllowed));
                });
            }

            foreach (var entry in allowed)
            {
                IPAddress parsedAllowed;
                if (IPAddress.TryParse(entry, out parsedAllowed) && parsedClient.Equals(parsedAllowed))
                {
                    return true;
                }

                if (string.Equals(entry, clientIp, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private string ResolveClientIp()
        {
            if (Request == null)
            {
                return null;
            }

            // Prefer direct connection address for console allowlist (do not trust X-Forwarded-For here).
            return Request.UserHostAddress;
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
                var clientIp = ResolveClientIp() ?? "unknown";
                var outcome = model.IsError ? "error" : "ok";
                var rowsPart = model.RowsAffected.HasValue
                    ? (" rowsAffected=" + model.RowsAffected.Value)
                    : "";
                var errorPart = model.IsError && !string.IsNullOrWhiteSpace(model.Message)
                    ? (" error=" + TruncateForAudit(model.Message, 2000))
                    : "";
                var durationPart = " durationMs=" + model.ExecutionTimeMs;
                var newValues = outcome + " ip=" + clientIp + rowsPart + durationPart + errorPart;

                _auditWriter.Write(
                    "DB_CONSOLE_EXECUTE",
                    "ApplicationUser",
                    userId,
                    TruncateForAudit(sql, AuditSqlMaxLength),
                    TruncateForAudit(newValues, 4000));
            }
            catch
            {
                // Audit must not block the console; platform admins may lack org context.
            }
        }

        private static string TruncateForAudit(string sql)
        {
            return TruncateForAudit(sql, AuditSqlMaxLength);
        }

        private static string TruncateForAudit(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (maxLength <= 0 || trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxLength) + "...";
        }
    }
}
