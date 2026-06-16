using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.App_Start
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                name: "Tenant",
                url: "{tenant}/{controller}/{action}/{id}",
                defaults: new { controller = "Dashboard", action = "Index", id = UrlParameter.Optional },
                constraints: new { tenant = new TenantRouteConstraint() },
                namespaces: new[] { "AssetManagement.Web.Controllers" }
            );

            routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Account", action = "Landing", id = UrlParameter.Optional },
                namespaces: new[] { "AssetManagement.Web.Controllers" }
            );
        }
    }
}
