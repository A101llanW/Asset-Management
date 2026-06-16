using System.Web;
using System.Web.Mvc;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.App_Start
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new PlatformAdminTenantRedirectAttribute());
            filters.Add(new HandleErrorAttribute());
            filters.Add(new BusinessExceptionFilter());
            filters.Add(new TenantFilterAttribute());
            filters.Add(new ImpersonationExpiryFilterAttribute());
        }
    }
}
