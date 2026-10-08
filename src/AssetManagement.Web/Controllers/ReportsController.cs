using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Reports.View")]
    public class ReportsController : BaseController
    {
        private readonly IReportService _reportService;
        private readonly IAuthorizationService _authorizationService;

        public ReportsController()
        {
            _reportService = BuildReportService();
            _authorizationService = BuildAuthorizationService();
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

                if (!CanAccessReportType(model.ReportType))
                {
                    return Json(new { success = false, message = "You do not have permission to run this report." });
                }

                model.ApplicationBaseUrl = ResolveApplicationBaseUrl();
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
            if (!CanAccessReportType(model == null ? null : model.ReportType))
            {
                return new HttpStatusCodeResult(403, "You do not have permission to run this report.");
            }

            var result = _reportService.GenerateReportDocument(model, ResolveGeneratedBy());
            return File(result.CsvBytes, "text/csv", result.FileName);
        }

        public ActionResult ExportAssetRegister()
        {
            return File(
                _reportService.ExportAssetRegisterCsv(),
                "text/csv",
                "asset-register-" + KenyaTimeHelper.LocalDateStamp(DateTime.UtcNow) + ".csv");
        }

        public ActionResult ExportCustodyMovement(DateTime? fromDate, DateTime? toDate)
        {
            return File(
                _reportService.ExportCustodyMovementCsv(fromDate, toDate),
                "text/csv",
                "custody-movement-" + KenyaTimeHelper.LocalDateStamp(DateTime.UtcNow) + ".csv");
        }

        public ActionResult ExportDepartmentSummary()
        {
            return File(
                _reportService.ExportDepartmentSummaryCsv(),
                "text/csv",
                "department-summary-" + KenyaTimeHelper.LocalDateStamp(DateTime.UtcNow) + ".csv");
        }

        public ActionResult ExportPendingApprovalsAging()
        {
            return File(
                _reportService.ExportPendingApprovalsAgingCsv(),
                "text/csv",
                "pending-approvals-aging-" + KenyaTimeHelper.LocalDateStamp(DateTime.UtcNow) + ".csv");
        }

        public ActionResult ExportGeneralLedger()
        {
            return File(
                _reportService.ExportGeneralLedgerCsv(),
                "text/csv",
                "general-ledger-" + KenyaTimeHelper.LocalDateStamp(DateTime.UtcNow) + ".csv");
        }

        private string ResolveGeneratedBy()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                return User.Identity.Name;
            }

            return "System";
        }

        private bool CanAccessReportType(string reportType)
        {
            var key = (reportType ?? string.Empty).Trim().ToLowerInvariant();
            if (key != "asset-depreciation")
            {
                return true;
            }

            if (_authorizationService == null)
            {
                return false;
            }

            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return false;
            }

            return _authorizationService.HasPermission(userId, "Depreciation.View")
                || _authorizationService.HasPermission(userId, "Financials.View");
        }
    }
}
