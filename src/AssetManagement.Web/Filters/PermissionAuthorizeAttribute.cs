using System;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Services;

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

            var principal = httpContext.User as ClaimsPrincipal;
            var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            using (var context = new AssetManagementDbContext())
            using (var uow = new UnitOfWork(context))
            {
                var authorizationService = new AuthorizationService(uow);
                return authorizationService.HasPermission(userId, _permissionCode);
            }
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                base.HandleUnauthorizedRequest(filterContext);
                return;
            }

            filterContext.Result = new HttpStatusCodeResult(403, "You do not have permission to access this action.");
        }
    }
}
