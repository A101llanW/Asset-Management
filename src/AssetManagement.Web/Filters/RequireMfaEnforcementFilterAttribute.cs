using System;
using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Application;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Filters
{
    /// <summary>
    /// Redirects authenticated users who must use MFA but have not enrolled yet.
    /// </summary>
    public class RequireMfaEnforcementFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null || !DeploymentSecuritySettings.RequireMfaForAllUsers)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var httpContext = filterContext.HttpContext;
            if (httpContext == null
                || httpContext.User == null
                || httpContext.User.Identity == null
                || !httpContext.User.Identity.IsAuthenticated)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var controllerName = filterContext.ActionDescriptor?.ControllerDescriptor?.ControllerName ?? string.Empty;
            var actionName = filterContext.ActionDescriptor?.ActionName ?? string.Empty;
            if (IsAccountMfaFlow(controllerName, actionName))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var accountSecurityService = DependencyResolver.Current.GetService<IAccountSecurityService>();
            if (accountSecurityService == null || !accountSecurityService.RequiresMfa(userId))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var user = new UserAccountRepository(connectionFactory).FindById(userId);
            if (user == null || user.TwoFactorEnabled)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            httpContext.Session["ForcedMfaSetupUserId"] = userId;

            var tenantSlug = TenantUrlHelper.GetTenantToken(filterContext.RouteData);
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                var unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
                tenantSlug = TenantUrlHelper.ResolveOrganizationSlug(unitOfWork, userId);
            }

            if (!string.IsNullOrWhiteSpace(tenantSlug))
            {
                filterContext.Result = new RedirectToRouteResult(
                    "Tenant",
                    new RouteValueDictionary(new
                    {
                        tenant = tenantSlug,
                        controller = "Account",
                        action = "SetupMfa"
                    }));
            }
            else
            {
                filterContext.Result = new RedirectToRouteResult(
                    "Default",
                    new RouteValueDictionary(new
                    {
                        controller = "Account",
                        action = "SetupMfa"
                    }));
            }
        }

        private static bool IsAccountMfaFlow(string controllerName, string actionName)
        {
            if (!string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(controllerName, "Captcha", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(controllerName, "Home", StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(actionName, "SetupMfa", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "VerifyMfa", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "SendSetupMfaCode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ResendMfaCode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "LogOff", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "Login", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "VerifyEmail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ChangePassword", StringComparison.OrdinalIgnoreCase);
        }
    }
}
