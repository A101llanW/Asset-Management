using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Security;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public sealed class ModuleAccessAttribute : AuthorizeAttribute
    {
        private readonly string _moduleKey;

        public ModuleAccessAttribute(string moduleKey)
        {
            _moduleKey = moduleKey;
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            // Custodian self-service actions (My Assets, Details of an asset the user holds): signed-in users pass
            // here and the action limits data to their own custody.
            if (AllowCustodianSelfServiceAttribute.AppliesToAuthenticated(filterContext))
            {
                return;
            }

            base.OnAuthorization(filterContext);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            // Signed-in but no module access: in-app Forbidden page (403) instead of a 401 that bounces to login.
            if (filterContext != null
                && filterContext.HttpContext != null
                && filterContext.HttpContext.User != null
                && filterContext.HttpContext.User.Identity != null
                && filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = ForbiddenResults.Create(filterContext);
                return;
            }

            base.HandleUnauthorizedRequest(filterContext);
        }

        protected override bool AuthorizeCore(System.Web.HttpContextBase httpContext)
        {
            if (httpContext == null || httpContext.User == null || !httpContext.User.Identity.IsAuthenticated)
            {
                return false;
            }

            var userId = httpContext.User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var authorizationService = DependencyResolver.Current.GetService<IAuthorizationService>();
            var moduleService = DependencyResolver.Current.GetService<IModulePermissionService>();
            if (authorizationService == null || moduleService == null)
            {
                return false;
            }

            return moduleService.UserHasModuleAccess(userId, _moduleKey, authorizationService);
        }
    }
}
