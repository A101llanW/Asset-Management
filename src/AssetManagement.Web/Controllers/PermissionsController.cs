using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Roles.View")]
    public class PermissionsController : BaseController
    {
        private readonly IPermissionService _permissionService;

        public PermissionsController()
        {
            _permissionService = BuildPermissionService();
        }

        public ActionResult Index()
        {
            return View(_permissionService.GetGroupedPermissions());
        }
    }
}
