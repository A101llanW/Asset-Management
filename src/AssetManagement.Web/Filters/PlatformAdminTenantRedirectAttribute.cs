using System.Web.Mvc;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Filters
{
    /// <summary>
    /// Runs before permission checks so platform admins are not blocked with 403 on tenant URLs.
    /// </summary>
    public sealed class PlatformAdminTenantRedirectAttribute : AuthorizeAttribute
    {
        public PlatformAdminTenantRedirectAttribute()
        {
            Order = -1;
        }

        public override void OnAuthorization(AuthorizationContext filterContext)
        {
            if (filterContext == null)
            {
                return;
            }

            ActionResult redirect;
            if (PlatformAdminHelper.TryCreateTenantPortalRedirect(filterContext, out redirect))
            {
                filterContext.Result = redirect;
            }
        }
    }
}
