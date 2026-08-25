using System.Web.Mvc;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Filters
{
    public static class TenantLoginRedirect
    {
        public static void RedirectToLogin(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                return;
            }

            var tenant = TenantUrlHelper.GetTenantToken(filterContext.RouteData);
            var returnUrl = filterContext.HttpContext.Request.RawUrl;
            if (LocalReturnUrlHelper.IsDefaultTenantLandingPath(returnUrl))
            {
                returnUrl = null;
            }

            filterContext.Result = TenantUrlHelper.CreateTenantLoginRedirect(tenant, returnUrl);
        }
    }
}
