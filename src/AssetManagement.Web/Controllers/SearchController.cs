using System.Web.Mvc;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
    public class SearchController : BaseController
    {
        public ActionResult Index(string q)
        {
            return RedirectToAction("Lookup", "AssetScan", new { q });
        }
    }
}
