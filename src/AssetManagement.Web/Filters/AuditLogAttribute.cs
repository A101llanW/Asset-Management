using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Filters
{
    public class AuditLogAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            if (filterContext == null || filterContext.IsChildAction)
            {
                base.OnActionExecuted(filterContext);
                return;
            }

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var action = filterContext.ActionDescriptor.ActionName;
            if (string.Equals(controller, "SecurityLogs", StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuted(filterContext);
                return;
            }

            if (string.Equals(controller, "Dashboard", StringComparison.OrdinalIgnoreCase)
                && (string.Equals(action, "GetImpersonationStatus", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, "GetMyImpersonationStatus", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, "GetPendingRequests", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(action, "CheckRequestStatus", StringComparison.OrdinalIgnoreCase)))
            {
                base.OnActionExecuted(filterContext);
                return;
            }

            if (!ShouldWriteHttpAudit(filterContext))
            {
                base.OnActionExecuted(filterContext);
                return;
            }

            var auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
            if (auditWriter == null)
            {
                base.OnActionExecuted(filterContext);
                return;
            }

            var user = filterContext.HttpContext.User;
            var actorId = user != null && user.Identity != null && user.Identity.IsAuthenticated
                ? user.GetUserId()
                : null;
            var actionType = string.Equals(filterContext.HttpContext.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase)
                ? "VIEW"
                : filterContext.HttpContext.Request.HttpMethod;
            var entityId = filterContext.RouteData.Values.ContainsKey("id")
                ? Convert.ToString(filterContext.RouteData.Values["id"])
                : null;
            var succeeded = filterContext.Exception == null;
            var detail = succeeded
                ? actionType + ":" + action
                : actionType + ":" + action + " - " + filterContext.Exception.Message;

            auditWriter.Write(
                "HTTP." + controller + "." + action,
                controller,
                entityId,
                actorId,
                detail);

            base.OnActionExecuted(filterContext);
        }

        private static bool ShouldWriteHttpAudit(ActionExecutedContext filterContext)
        {
            var areaName = filterContext.RouteData.DataTokens.ContainsKey("area")
                ? filterContext.RouteData.DataTokens["area"] as string
                : null;
            if (string.Equals(areaName, "Platform", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            return organizationScope != null && organizationScope.GetCurrentOrganizationId().HasValue;
        }
    }
}
