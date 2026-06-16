using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("SecurityLogs.View")]
    public class SecurityLogsController : BaseController
    {
        private readonly ISecurityLogService _securityLogService;

        public SecurityLogsController()
        {
            _securityLogService = DependencyResolver.Current.GetService<ISecurityLogService>();
        }

        public ActionResult Index(SecurityLogFilterVm filter, string tab)
        {
            var model = _securityLogService == null
                ? new SecurityLogsPageVm { Filter = filter ?? new SecurityLogFilterVm() }
                : _securityLogService.GetLogs(filter ?? new SecurityLogFilterVm(), false);
            ViewBag.ActiveTab = string.IsNullOrWhiteSpace(tab) ? "login" : tab;
            return View(model);
        }
    }
}
