using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Roles.View")]
    public class PermissionsController : BaseController
    {
        private readonly IModulePermissionService _modulePermissionService;
        private readonly IPermissionService _permissionService;

        public PermissionsController()
        {
            _modulePermissionService = DependencyResolver.Current.GetService<IModulePermissionService>();
            _permissionService = BuildPermissionService();
        }

        public ActionResult Index()
        {
            if (_modulePermissionService != null)
            {
                return View(_modulePermissionService.GetModuleGroupedPermissions());
            }

            return View(_permissionService.GetGroupedPermissions());
        }
    }
}
