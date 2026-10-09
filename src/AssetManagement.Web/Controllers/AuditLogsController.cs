using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
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

        public ActionResult Index(AuditLogFilterVm filter)
        {
            filter = PrepareFilter(filter);
            if (filter.RelatedAssetId.HasValue)
            {
                var assetDetails = BuildAssetService().GetById(filter.RelatedAssetId.Value);
                if (assetDetails == null)
                {
                    return HttpNotFound();
                }

                ViewBag.RelatedAssetTag = assetDetails.AssetTag;
                ViewBag.RelatedAssetName = assetDetails.AssetName;
                ViewBag.BackUrl = Url.Action("Details", "Assets", new { id = assetDetails.Id });
                ViewBag.Filter = filter;
                var related = new AuditLogPageVm();
                foreach (var log in _auditLogService.GetLogs(filter))
                {
                    related.Items.Add(log);
                }

                related.Page = 1;
                related.PageSize = related.Items.Count;
                related.TotalCount = related.Items.Count;
                return View(related);
            }

            ViewBag.Filter = filter;
            return View(_auditLogService.GetLogPage(filter));
        }

        [PermissionAuthorize("AuditLogs.Export")]
        public ActionResult Export(AuditLogFilterVm filter)
        {
            filter = PrepareFilter(filter);
            return File(
                _auditLogService.ExportCsv(filter),
                "text/csv",
                "audit-log-" + KenyaTimeHelper.LocalDateStamp(DateTime.UtcNow) + ".csv");
        }

        /// <summary>
        /// The model binder fills AuditLogFilterVm.Action from the route value {action} ("Index"/"Export"),
        /// which filtered every list to Action = 'Index' and showed no data. The action-code filter is taken from
        /// the ActionCode (or legacy ?Action=) query value only. Every user here holds AuditLogs.View, so the
        /// list is organization-wide (rel1009g).
        /// </summary>
        private AuditLogFilterVm PrepareFilter(AuditLogFilterVm filter)
        {
            filter = filter ?? new AuditLogFilterVm();
            filter.Action = AuditLogFilterHelper.ResolveActionFilter(filter.ActionCode, Request.QueryString["Action"]);
            filter.ActionCode = filter.Action;
            filter.OrganizationWide = !filter.RelatedAssetId.HasValue;
            filter.Page = AuditLogFilterHelper.NormalizePage(filter.Page);
            filter.PageSize = AuditLogFilterHelper.NormalizePageSize(filter.PageSize);
            filter.MaxRows = null;
            filter.BusinessEventsOnly = false;
            return filter;
        }
    }
}
