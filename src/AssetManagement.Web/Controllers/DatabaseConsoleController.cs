using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
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
    /// rel1009i: DatabaseConsoleAllowedIps = "*" or "any" (case-insensitive, alone or as one list entry) disables the
    /// IP check; the Platform Admin gate and login/MFA still apply, audit rows add "allowlist=disabled" and the page
    /// shows a warning banner. Blank/missing still means loopback only.
    /// Audit (AuditLog): DB_CONSOLE_VIEW for every allowed page view, DB_CONSOLE_DENIED for every refused attempt
    /// (unauthenticated, not Platform Admin, IP not allowlisted; written before the redirect), DB_CONSOLE_EXECUTE
    /// for SQL execution. A logging failure never breaks the page.
    /// </summary>
    [Authorize]
    public class DatabaseConsoleController : Controller
    {
        private const int AuditSqlMaxLength = 8000;
        private const string AllowedIpsAppSettingKey = "DatabaseConsoleAllowedIps";
        private const string AuditViewAction = "DB_CONSOLE_VIEW";
        private const string AuditDeniedAction = "DB_CONSOLE_DENIED";

        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;
        private readonly DatabaseConsoleService _consoleService;

        public DatabaseConsoleController()
        {
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
            _consoleService = DependencyResolver.Current.GetService<DatabaseConsoleService>();
        }

        /// <summary>
        /// Runs before [Authorize] and [ValidateAntiForgeryToken], so every refused attempt (including anonymous
        /// ones) is audited before the redirect to Login/Forbidden.
        /// </summary>
        protected override void OnAuthorization(AuthorizationContext filterContext)
        {
            base.OnAuthorization(filterContext);
            if (filterContext == null || filterContext.Result != null)
            {
                return;
            }

            var gate = GateConsoleAccess();
            if (gate != null)
            {
                filterContext.Result = gate;
            }
        }

        public ActionResult Index()
        {
            var gate = GateConsoleAccess();
            if (gate != null)
            {
                return gate;
            }

            TryAuditConsole(AuditViewAction, "allowed");
            var model = new DatabaseConsoleViewModel();
            model.AllowlistDisabled = IsAllowlistDisabled();
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
            model.AllowlistDisabled = IsAllowlistDisabled();
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
                TryAuditConsole(AuditDeniedAction, "refused reason=unauthenticated");
                var returnUrl = Request != null ? Request.RawUrl : AssetManagement.Web.Helpers.AppPathHelper.ToAppAbsolute("~/Sys/Database");
                return RedirectToAction("Login", "Account", new { returnUrl = returnUrl });
            }

            if (_organizationScope == null || !_organizationScope.IsActualPlatformAdmin())
            {
                TryAuditConsole(AuditDeniedAction, "refused reason=not_platform_admin");
                TempData["ErrorMessage"] = "The database console is restricted to Platform Admin (SuperAdmin).";
                return RedirectToAction("Forbidden", "Home");
            }

            if (!IsClientIpAllowed())
            {
                TryAuditConsole(AuditDeniedAction, "refused reason=ip_not_allowlisted");
                TempData["ErrorMessage"] = "The database console is restricted by IP allowlist.";
                return RedirectToAction("Forbidden", "Home");
            }

            return null;
        }

        private bool IsClientIpAllowed()
        {
            return DatabaseConsoleIpAllowlist.IsAllowed(
                ConfigurationManager.AppSettings[AllowedIpsAppSettingKey],
                ResolveClientIp());
        }

        private static bool IsAllowlistDisabled()
        {
            return DatabaseConsoleIpAllowlist.IsDisabled(ConfigurationManager.AppSettings[AllowedIpsAppSettingKey]);
        }

        private static string AllowlistAuditSuffix()
        {
            return IsAllowlistDisabled() ? " allowlist=disabled" : "";
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
                var newValues = outcome + " ip=" + clientIp + AllowlistAuditSuffix() + rowsPart + durationPart + errorPart;

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

        /// <summary>
        /// Console page view / refusal audit. Uses IAuditWriter (as DB_CONSOLE_EXECUTE) for signed-in users; when there
        /// is no user or the writer cannot resolve an organization, inserts the AuditLog row directly with
        /// OrganizationId NULL (platform-level event). Never throws.
        /// </summary>
        private void TryAuditConsole(string action, string outcome)
        {
            try
            {
                var authenticated = User != null && User.Identity != null && User.Identity.IsAuthenticated;
                var userId = authenticated ? User.GetUserId() : null;
                var userName = authenticated && !string.IsNullOrWhiteSpace(User.Identity.Name) ? User.Identity.Name : "anonymous";
                var clientIp = ResolveClientIp() ?? "unknown";
                var method = Request != null ? Request.HttpMethod : null;
                var path = Request != null && Request.Url != null ? Request.Url.AbsolutePath : null;
                var newValues = TruncateForAudit(
                    outcome + " user=" + userName + " ip=" + clientIp + AllowlistAuditSuffix() + " method=" + method + " path=" + path,
                    4000);

                if (!string.IsNullOrWhiteSpace(userId) && _auditWriter != null)
                {
                    try
                    {
                        _auditWriter.Write(action, "ApplicationUser", userId, null, newValues);
                        return;
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning("DatabaseConsole audit via IAuditWriter failed, writing directly: " + ex.Message);
                    }
                }

                InsertAuditRowDirect(action, userId, newValues, clientIp);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("DatabaseConsole audit failed for " + action + ": " + ex.Message);
            }
        }

        private static void InsertAuditRowDirect(string action, string userId, string newValues, string clientIp)
        {
            var connectionFactory = DependencyResolver.Current.GetService<AssetManagement.Infrastructure.Persistence.ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return;
            }

            using (var connection = connectionFactory.CreateConnection())
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
INSERT INTO [AuditLog] ([ActorUserId],[Action],[EntityType],[EntityId],[OldValues],[NewValues],[Timestamp],[IPAddress],[CreatedAt],[IsActive],[OrganizationId])
VALUES (@ActorUserId,@Action,@EntityType,@EntityId,NULL,@NewValues,@Now,@IPAddress,@Now,1,NULL)";
                    var now = DateTime.UtcNow;
                    AddAuditParameter(command, "@ActorUserId", string.IsNullOrWhiteSpace(userId) ? null : userId);
                    AddAuditParameter(command, "@Action", action);
                    AddAuditParameter(command, "@EntityType", string.IsNullOrWhiteSpace(userId) ? null : "ApplicationUser");
                    AddAuditParameter(command, "@EntityId", string.IsNullOrWhiteSpace(userId) ? null : userId);
                    AddAuditParameter(command, "@NewValues", newValues);
                    AddAuditParameter(command, "@Now", now);
                    AddAuditParameter(command, "@IPAddress", TruncateForAudit(clientIp, 100));
                    command.ExecuteNonQuery();
                }
            }
        }

        private static void AddAuditParameter(IDbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
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

    /// <summary>
    /// Pure DatabaseConsoleAllowedIps evaluation (unit-tested). "*" or "any" (case-insensitive, alone or as one
    /// entry of a , ; or space separated list) disables the IP check. Blank/missing = loopback only.
    /// </summary>
    public static class DatabaseConsoleIpAllowlist
    {
        public const string DefaultAllowedIps = "127.0.0.1,::1";

        public static string[] ParseEntries(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = DefaultAllowedIps;
            }

            var entries = raw.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();
            return entries.Length == 0 ? new[] { "127.0.0.1", "::1" } : entries;
        }

        public static bool IsDisabled(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            return ParseEntries(raw).Any(e =>
                string.Equals(e, "*", StringComparison.Ordinal)
                || string.Equals(e, "any", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsAllowed(string raw, string clientIp)
        {
            if (IsDisabled(raw))
            {
                return true;
            }

            var allowed = ParseEntries(raw);
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
    }
}
