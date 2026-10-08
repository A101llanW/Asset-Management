using System.Web.Mvc;

namespace AssetManagement.Web.Filters
{
    public class TenantAuthorizeAttribute : AuthorizeAttribute
    {
        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (filterContext == null ||
                filterContext.HttpContext == null ||
                filterContext.HttpContext.User == null ||
                !filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                TenantLoginRedirect.RedirectToLogin(filterContext);
                return;
            }

            filterContext.Result = ForbiddenResults.Create(filterContext);
        }
    }
}
