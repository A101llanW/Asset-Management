using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Filters
{
    public class ImpersonationExpiryFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var httpContext = filterContext.HttpContext;
            if (httpContext == null ||
                httpContext.User == null ||
                !httpContext.User.Identity.IsAuthenticated ||
                httpContext.Session == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            if (!ImpersonationSessionHelper.IsSessionImpersonating(httpContext.Session))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            if (organizationScope == null || !organizationScope.IsActualPlatformAdmin())
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var actionName = filterContext.ActionDescriptor.ActionName;
            var areaName = filterContext.RouteData.DataTokens.ContainsKey("area")
                ? filterContext.RouteData.DataTokens["area"] as string
                : null;
            var whitelisted = IsWhitelisted(areaName, controllerName, actionName);

            int? organizationId = httpContext.Session["ImpersonatedOrganizationId"] as int?;
            var unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            var auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
            var actorName = httpContext.User.Identity.Name;
            var requestActive = false;

            if (unitOfWork != null)
            {
                var request = ImpersonationSessionHelper.GetSessionRequest(httpContext.Session, unitOfWork);
                requestActive = ImpersonationSessionHelper.IsRequestActive(request);

                if (!requestActive)
                {
                    ImpersonationSessionHelper.TryClearStaleImpersonationSession(
                        httpContext.Session,
                        unitOfWork,
                        auditWriter,
                        actorName);

                    if (!whitelisted)
                    {
                        var urlHelper = new UrlHelper(filterContext.RequestContext);
                        var redirectUrl = ImpersonationSessionHelper.BuildPlatformAdminPostExpiryUrl(urlHelper, organizationId)
                            ?? "/Platform/Organizations/Index";
                        filterContext.Result = new RedirectResult(redirectUrl);
                        return;
                    }
                }
            }

            base.OnActionExecuting(filterContext);
        }

        private static bool IsWhitelisted(string areaName, string controllerName, string actionName)
        {
            if (string.Equals(controllerName, "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "GetMyImpersonationStatus", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "GetImpersonationStatus", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "GetPendingRequests", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(areaName, "Platform", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(controllerName, "Organizations", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "StopImpersonating", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "GetMyImpersonationStatus", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "Index", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "OrganizationDetails", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "CheckRequestStatus", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "Elevate", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "RequestImpersonation", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(actionName, "CancelImpersonationRequest", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}
