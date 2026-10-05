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
        public const string PlatformAdminRoleName = AssetManagement.Application.Security.SystemRoleNames.PlatformAdmin;

        public const string PlatformOrganizationsIndexPath = "/Platform/Organizations/Index";

        /// <summary>App-absolute URL for the platform organizations list (prefixed with the IIS sub-app path, e.g. /NanoAssetSuit).</summary>
        public static string PlatformOrganizationsIndexUrl
        {
            get { return AppPathHelper.ToAppAbsolute(PlatformOrganizationsIndexPath); }
        }

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

            string roleName;
            bool isSystemRole;
            if (!users.TryGetRoleInfoByUserId(userId, out roleName, out isSystemRole))
            {
                return false;
            }

            return AssetManagement.Application.Security.SystemRoleNames.IsPlatformAdmin(roleName, isSystemRole);
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
            return new RedirectResult(PlatformOrganizationsIndexUrl);
        }
    }
}
