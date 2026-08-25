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
