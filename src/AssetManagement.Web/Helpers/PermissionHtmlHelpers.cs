using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Services;
using AssetManagement.Infrastructure.Security;

namespace AssetManagement.Web.Helpers
{
    public static class PermissionHtmlHelpers
    {
        public static string ToFriendlyPermissionLabel(string permissionCode, string fallbackName = null)
        {
            if (!string.IsNullOrWhiteSpace(fallbackName))
            {
                return fallbackName.Trim();
            }

            if (string.IsNullOrWhiteSpace(permissionCode))
            {
                return string.Empty;
            }

            var parts = permissionCode.Split('.');
            if (parts.Length != 2)
            {
                return permissionCode;
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
                case "Download":
                    return "Download " + module;
                case "Export":
                    return "Export " + module;
                default:
                    return action + " " + module;
            }
        }

        public static bool HasPermission(this HtmlHelper htmlHelper, string permissionCode)
        {
            return HasPermissionCore(permissionCode);
        }

        public static bool HasPermission<TModel>(this HtmlHelper<TModel> htmlHelper, string permissionCode)
        {
            return HasPermissionCore(permissionCode);
        }

        public static bool CanUploadAssetDocument(this HtmlHelper htmlHelper, string assetCustodianId)
        {
            return CanUploadAssetDocumentCore(assetCustodianId);
        }

        public static bool CanUploadAssetDocument<TModel>(this HtmlHelper<TModel> htmlHelper, string assetCustodianId)
        {
            return CanUploadAssetDocumentCore(assetCustodianId);
        }

        public static bool CanDownloadAssetDocument(this HtmlHelper htmlHelper, string assetCustodianId)
        {
            return CanDownloadAssetDocumentCore(assetCustodianId);
        }

        public static bool CanDownloadAssetDocument<TModel>(this HtmlHelper<TModel> htmlHelper, string assetCustodianId)
        {
            return CanDownloadAssetDocumentCore(assetCustodianId);
        }

        private static bool HasPermissionCore(string permissionCode)
        {
            var userId = FormsAuthHelper.GetUserId(HttpContext.Current != null ? HttpContext.Current.User : null);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var auth = DependencyResolver.Current.GetService<IAuthorizationService>();
            return auth != null && auth.HasPermission(userId, permissionCode);
        }

        private static bool CanUploadAssetDocumentCore(string assetCustodianId)
        {
            if (HasPermissionCore("Documents.Upload"))
            {
                return true;
            }

            var userId = FormsAuthHelper.GetUserId(HttpContext.Current != null ? HttpContext.Current.User : null);
            return AssetDocumentAccessRules.IsCurrentCustodian(assetCustodianId, userId);
        }

        private static bool CanDownloadAssetDocumentCore(string assetCustodianId)
        {
            if (HasPermissionCore("Documents.Download"))
            {
                return true;
            }

            var userId = FormsAuthHelper.GetUserId(HttpContext.Current != null ? HttpContext.Current.User : null);
            return AssetDocumentAccessRules.IsCurrentCustodian(assetCustodianId, userId);
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
                case "Insurance":
                    return "insurance policy";
                case "SecurityLogs":
                    return "security log";
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
                    return ChooseIndefiniteArticle(noun) + noun;
            }
        }

        private static string ChooseIndefiniteArticle(string noun)
        {
            if (string.IsNullOrWhiteSpace(noun))
            {
                return string.Empty;
            }

            var first = char.ToLowerInvariant(noun[0]);
            if (first == 'a' || first == 'e' || first == 'i' || first == 'o' || first == 'u')
            {
                return "an ";
            }

            return "a ";
        }
    }
}
