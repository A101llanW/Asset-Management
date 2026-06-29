using System.Web.Mvc;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Filters
{
    /// <summary>
    /// Ensures the request uses a valid tenant portal URL (/{tenant}/...).
    /// </summary>
    public sealed class RequireTenantRouteAttribute : ActionFilterAttribute
    {
        public RequireTenantRouteAttribute()
        {
            Order = 10;
        }

        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var tenantToken = TenantUrlHelper.GetTenantToken(filterContext.RouteData);
            if (!TenantUrlHelper.IsValidTenantSlug(tenantToken))
            {
                filterContext.Result = BuildDeniedResult(filterContext, "Use your organization portal URL (for example /nanosoft/AssetScan/Lookup).");
                return;
            }

            var organizationId = filterContext.HttpContext.Items[TenantContextKeys.OrganizationId] as int?;
            if (!organizationId.HasValue)
            {
                filterContext.Result = BuildDeniedResult(filterContext, "Organization portal could not be resolved for this URL.");
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        private static ActionResult BuildDeniedResult(ActionExecutingContext filterContext, string message)
        {
            if (IsJsonRequest(filterContext))
            {
                filterContext.HttpContext.Response.StatusCode = 404;
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                return new JsonResult
                {
                    Data = new
                    {
                        Found = false,
                        Message = message,
                        error = "tenant_required"
                    },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            return new HttpNotFoundResult(message);
        }

        private static bool IsJsonRequest(ActionExecutingContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            if (request.IsAjaxRequest())
            {
                return true;
            }

            var controller = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var action = filterContext.ActionDescriptor.ActionName;
            return string.Equals(controller, "AssetScan", System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(action, "LookupJson", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
