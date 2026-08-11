using System.Web;
using System.Web.Mvc;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.App_Start
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new AntiForgeryExceptionFilterAttribute());
            filters.Add(new PlatformAdminTenantRedirectAttribute());
            filters.Add(new HandleErrorAttribute());
            filters.Add(new BusinessExceptionFilter());
            filters.Add(new NoCacheAttribute());
            filters.Add(new AuditLogAttribute());
            filters.Add(new TenantFilterAttribute());
            filters.Add(new RequireMfaEnforcementFilterAttribute());
            filters.Add(new ImpersonationExpiryFilterAttribute());
            if (IsFreshPortalModeEnabled())
            {
                filters.Add(new FreshPortalSecurityAttribute());
            }
        }

        private static bool IsFreshPortalModeEnabled()
        {
            var setting = System.Configuration.ConfigurationManager.AppSettings["FreshPortalMode"];
            return string.Equals(setting, "true", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
