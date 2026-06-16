using System.Web.Mvc;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Depreciation posting is disabled; retained only to block legacy bookmarks.
    /// </summary>
    [PermissionAuthorize("Depreciation.View")]
    public class DepreciationController : BaseController
    {
        public ActionResult Index()
        {
            return new HttpNotFoundResult("Depreciation posting is not available.");
        }
    }
}
