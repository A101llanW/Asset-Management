using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Reports.View")]
    public class ReportsController : BaseController
    {
        private readonly IReportService _reportService;

        public ReportsController()
        {
            _reportService = BuildReportService();
        }

        public ActionResult Index()
        {
            var model = _reportService.GetDashboard();
            return View(model);
        }
    }
}
