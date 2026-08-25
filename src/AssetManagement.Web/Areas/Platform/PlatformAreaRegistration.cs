using System.Web.Mvc;

namespace AssetManagement.Web.Areas.Platform
{
    public class PlatformAreaRegistration : AreaRegistration
    {
        public override string AreaName
        {
            get { return "Platform"; }
        }

        public override void RegisterArea(AreaRegistrationContext context)
        {
            context.MapRoute(
                "Platform_default",
                "Platform/{controller}/{action}/{id}",
                new { controller = "Organizations", action = "Index", id = UrlParameter.Optional },
                new[] { "AssetManagement.Web.Areas.Platform.Controllers" });
        }
    }
}
