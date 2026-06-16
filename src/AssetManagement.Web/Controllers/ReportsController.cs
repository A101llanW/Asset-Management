using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
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
            return View(_reportService.GetReportsHub());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Preview(ReportExportRequestVm model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.ReportType))
                {
                    return Json(new { success = false, message = "Report type is required." });
                }

                var result = _reportService.GenerateReportDocument(model, ResolveGeneratedBy());
                return Json(new { success = true, html = result.Html, rowCount = result.RowCount, title = result.Title });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Reports.Export")]
        public ActionResult Export(ReportExportRequestVm model)
        {
            var result = _reportService.GenerateReportDocument(model, ResolveGeneratedBy());
            return File(result.CsvBytes, "text/csv", result.FileName);
        }

        public ActionResult ExportAssetRegister()
        {
            return File(
                _reportService.ExportAssetRegisterCsv(),
                "text/csv",
                "asset-register-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        public ActionResult ExportCustodyMovement(DateTime? fromDate, DateTime? toDate)
        {
            return File(
                _reportService.ExportCustodyMovementCsv(fromDate, toDate),
                "text/csv",
                "custody-movement-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        public ActionResult ExportDepartmentSummary()
        {
            return File(
                _reportService.ExportDepartmentSummaryCsv(),
                "text/csv",
                "department-summary-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        public ActionResult ExportPendingApprovalsAging()
        {
            return File(
                _reportService.ExportPendingApprovalsAgingCsv(),
                "text/csv",
                "pending-approvals-aging-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        public ActionResult ExportGeneralLedger()
        {
            return File(
                _reportService.ExportGeneralLedgerCsv(),
                "text/csv",
                "general-ledger-" + DateTime.UtcNow.ToString("yyyyMMdd") + ".csv");
        }

        private string ResolveGeneratedBy()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                return User.Identity.Name;
            }

            return "System";
        }
    }
}
