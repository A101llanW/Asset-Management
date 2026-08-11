using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Filters
{
    public class FreshPortalSecurityAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null || filterContext.HttpContext.Session == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var isFreshPortalSession = filterContext.HttpContext.Session["IsFreshPortalSession"] as bool?;
            if (isFreshPortalSession != true)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var path = filterContext.HttpContext.Request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
            var restrictedPaths = new[]
            {
                "~/platform/",
                "~/platform/organizations/",
                "~/platform/licenses/",
                "~/platform/securitylogs/"
            };

            foreach (var restricted in restrictedPaths)
            {
                if (path.StartsWith(restricted, StringComparison.OrdinalIgnoreCase))
                {
                    filterContext.Result = new ViewResult
                    {
                        ViewName = "~/Views/Shared/FreshPortalRestriction.cshtml"
                    };
                    return;
                }
            }

            if (filterContext.HttpContext.User != null
                && filterContext.HttpContext.User.Identity != null
                && filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                var userId = filterContext.HttpContext.User.GetUserId();
                var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
                if (!string.IsNullOrWhiteSpace(userId) && connectionFactory != null)
                {
                    var user = new UserAccountRepository(connectionFactory).FindById(userId);
                    if (user != null && !user.OrganizationId.HasValue)
                    {
                        AuthSessionHelper.SignOut(filterContext.HttpContext);
                        filterContext.Result = new ViewResult
                        {
                            ViewName = "~/Views/Shared/FreshPortalRestriction.cshtml"
                        };
                        return;
                    }
                }
            }

            base.OnActionExecuting(filterContext);
        }
    }
}
