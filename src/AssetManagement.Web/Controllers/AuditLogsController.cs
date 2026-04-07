using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("AuditLogs.View")]
    public class AuditLogsController : BaseController
    {
        private readonly IAuditLogService _auditLogService;

        public AuditLogsController()
        {
            _auditLogService = BuildAuditLogService();
        }

        public ActionResult Index(AssetManagement.Application.ViewModels.AuditLogFilterVm filter)
        {
            return View(_auditLogService.GetLogs(filter));
        }
    }
}
