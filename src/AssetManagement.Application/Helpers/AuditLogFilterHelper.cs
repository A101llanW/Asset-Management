using System;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Audit Logs page filter rules (rel1009g): paging bounds, the Action filter that must not come from the MVC
    /// route value {action}, and what counts as an HTTP page-view row.
    /// </summary>
    public static class AuditLogFilterHelper
    {
        public const int DefaultPageSize = 50;
        public const int MinPageSize = 10;
        public const int MaxPageSize = 200;
        public const int MaxExportRows = 50000;

        public const string LoginSucceededAction = "Account.LoginSucceeded";
        public const string LoginFailedAction = "Account.LoginFailed";
        public const string LoginEntityType = "Account";

        public static int NormalizePage(int page)
        {
            return page < 1 ? 1 : page;
        }

        public static int NormalizePageSize(int pageSize)
        {
            if (pageSize <= 0)
            {
                return DefaultPageSize;
            }

            if (pageSize < MinPageSize)
            {
                return MinPageSize;
            }

            return pageSize > MaxPageSize ? MaxPageSize : pageSize;
        }

        /// <summary>
        /// The action-code filter comes from the ActionCode query value (or a legacy ?Action= query value),
        /// never from the route, so /AuditLogs/Index no longer filters on Action = 'Index'.
        /// </summary>
        public static string ResolveActionFilter(string actionCode, string legacyQueryAction)
        {
            var value = string.IsNullOrWhiteSpace(actionCode) ? legacyQueryAction : actionCode;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>HTTP GET page hits written by AuditLogAttribute ("HTTP.Controller.Action" with "VIEW:...").</summary>
        public static bool IsPageView(string action, string newValues)
        {
            return !string.IsNullOrEmpty(action)
                && action.StartsWith("HTTP.", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrEmpty(newValues)
                && newValues.StartsWith("VIEW:", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatActor(string actorUserId, string firstName, string lastName, string email)
        {
            var name = ((firstName ?? string.Empty).Trim() + " " + (lastName ?? string.Empty).Trim()).Trim();
            var mail = (email ?? string.Empty).Trim();
            if (name.Length > 0 && mail.Length > 0)
            {
                return name + " (" + mail + ")";
            }

            if (name.Length > 0)
            {
                return name;
            }

            if (mail.Length > 0)
            {
                return mail;
            }

            return string.IsNullOrWhiteSpace(actorUserId) ? "System" : actorUserId.Trim();
        }
    }
}
