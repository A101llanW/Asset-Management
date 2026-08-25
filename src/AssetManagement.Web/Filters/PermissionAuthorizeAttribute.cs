using System;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Filters
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class PermissionAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string _permissionCode;

        public PermissionAuthorizeAttribute(string permissionCode)
        {
            _permissionCode = permissionCode;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (!httpContext.User.Identity.IsAuthenticated)
            {
                return false;
            }

            var userId = FormsAuthHelper.GetUserId(httpContext.User);
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var authorizationService = DependencyResolver.Current.GetService<IAuthorizationService>();
            if (authorizationService == null)
            {
                return false;
            }

            return authorizationService.HasPermission(userId, _permissionCode);
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                TenantLoginRedirect.RedirectToLogin(filterContext);
                return;
            }

            if (TryRedirectPlatformAdminFromTenantPortal(filterContext))
            {
                return;
            }

            filterContext.Result = new HttpStatusCodeResult(403, "You do not have permission to access this action.");
        }

        private static bool TryRedirectPlatformAdminFromTenantPortal(AuthorizationContext filterContext)
        {
            ActionResult redirect;
            if (!PlatformAdminHelper.TryCreateTenantPortalRedirect(filterContext, out redirect))
            {
                return false;
            }

            filterContext.Result = redirect;
            return true;
        }
    }
}
