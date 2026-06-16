using System;
using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Application.Contracts;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Helpers
{
    public static class PlatformAdminHelper
    {
        public const string PlatformAdminRoleName = "Platform Admin";

        public const string PlatformOrganizationsIndexPath = "/Platform/Organizations/Index";

        public static bool IsPlatformAdmin(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return false;
            }

            var users = new UserAccountRepository(connectionFactory);
            var user = users.FindById(userId);
            if (user == null || !user.IsActive)
            {
                return false;
            }

            var roleName = users.FindRoleNameByUserId(userId);
            return string.Equals(roleName, PlatformAdminRoleName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPlatformAdmin(System.Security.Principal.IPrincipal principal)
        {
            return principal != null
                && principal.Identity != null
                && principal.Identity.IsAuthenticated
                && IsPlatformAdmin(FormsAuthHelper.GetUserId(principal));
        }

        public static bool TryCreateTenantPortalRedirect(ControllerContext context, out ActionResult redirect)
        {
            redirect = null;
            if (context == null || context.HttpContext == null || context.RouteData == null)
            {
                return false;
            }

            if (!IsPlatformAdmin(context.HttpContext.User))
            {
                return false;
            }

            var organizationScope = DependencyResolver.Current.GetService<Application.Contracts.Security.IOrganizationScopeService>();
            if (organizationScope != null && organizationScope.IsImpersonating())
            {
                return false;
            }

            var areaName = context.RouteData.DataTokens.ContainsKey("area")
                ? context.RouteData.DataTokens["area"] as string
                : null;
            if (string.Equals(areaName, "Platform", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var tenantToken = TenantUrlHelper.GetTenantToken(context.RouteData);
            if (string.IsNullOrWhiteSpace(tenantToken))
            {
                return false;
            }

            redirect = CreateOrganizationsRedirect();
            return true;
        }

        public static ActionResult CreateOrganizationsRedirect()
        {
            return new RedirectResult(PlatformOrganizationsIndexPath);
        }
    }
}
