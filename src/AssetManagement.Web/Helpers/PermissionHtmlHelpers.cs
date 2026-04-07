using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Services;

namespace AssetManagement.Web.Helpers
{
    public static class PermissionHtmlHelpers
    {
        public static string ToFriendlyPermissionLabel(string permissionCode, string fallbackName = null)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
            {
                return fallbackName ?? string.Empty;
            }

            var parts = permissionCode.Split('.');
            if (parts.Length != 2)
            {
                return fallbackName ?? permissionCode;
            }

            var module = ToFriendlyModuleName(parts[0]);
            var action = parts[1];

            switch (action)
            {
                case "View":
                    return "View " + module;
                case "Create":
                    return "Create " + WithArticle(module);
                case "Edit":
                    return "Edit " + WithArticle(module);
                case "Delete":
                    return "Delete " + WithArticle(module);
                case "Assign":
                    return "Assign " + WithArticle(module);
                case "Transfer":
                    return "Transfer " + WithArticle(module);
                case "Return":
                    return "Return " + WithArticle(module);
                case "Receive":
                    return "Receive " + WithArticle(module);
                case "Dispose":
                    return "Dispose " + WithArticle(module);
                case "Approve":
                    return "Approve " + module;
                case "ApproveDisposal":
                    return "Approve asset disposal";
                case "Manage":
                    return "Manage " + module;
                case "Upload":
                    return "Upload " + module;
                case "Export":
                    return "Export " + module;
                default:
                    return fallbackName ?? (action + " " + module);
            }
        }

        public static bool HasPermission(this HtmlHelper htmlHelper, string permissionCode)
        {
            var user = HttpContext.Current?.User as ClaimsPrincipal;
            var userId = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            using (var context = new AssetManagementDbContext())
            using (var unitOfWork = new UnitOfWork(context))
            {
                var auth = new AuthorizationService(unitOfWork);
                return auth.HasPermission(userId, permissionCode);
            }
        }

        private static string ToFriendlyModuleName(string module)
        {
            switch (module)
            {
                case "Assets":
                    return "asset";
                case "Users":
                    return "user";
                case "Roles":
                    return "role";
                case "Departments":
                    return "department";
                case "Suppliers":
                    return "supplier";
                case "Purchases":
                    return "purchase record";
                case "Incidents":
                    return "incident";
                case "Claims":
                    return "claim";
                case "Financials":
                    return "financial data";
                case "Depreciation":
                    return "depreciation";
                case "Documents":
                    return "documents";
                case "Reports":
                    return "reports";
                case "AuditLogs":
                case "Audit":
                    return "audit logs";
                case "Settings":
                    return "settings";
                case "Permissions":
                    return "permissions";
                default:
                    return module.ToLowerInvariant();
            }
        }

        private static string WithArticle(string noun)
        {
            if (string.IsNullOrWhiteSpace(noun))
            {
                return noun;
            }

            switch (noun)
            {
                case "documents":
                case "reports":
                case "audit logs":
                case "settings":
                case "permissions":
                case "financial data":
                case "depreciation":
                    return noun;
                default:
                    return "an " + noun;
            }
        }
    }
}
