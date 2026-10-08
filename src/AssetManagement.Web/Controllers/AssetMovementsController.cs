using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    /// <summary>Assets &amp; custody &gt; Movements: filterable, server-side paged movement history with batch view and CSV export.</summary>
    [PermissionAuthorize("Assets.Movements.View")]
    public class AssetMovementsController : BaseController
    {
        private IAssetMovementService BuildMovementService()
        {
            return DependencyResolver.Current.GetService<IAssetMovementService>();
        }

        public ActionResult Index(AssetMovementFilterVm filter)
        {
            filter = filter ?? new AssetMovementFilterVm();
            var model = BuildMovementService().GetPage(filter);
            ViewBag.DepartmentGroups = BuildMoveTargetDepartmentGroups(null, filter.DepartmentId);
            ViewBag.MovementTypes = Enum.GetValues(typeof(AssetMovementType));
            return View(model);
        }

        /// <summary>All movements written by one bulk action (bulk Move to department, Move group to class/room, import).</summary>
        public ActionResult Batch(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return RedirectToAction("Index");
            }

            var filter = new AssetMovementFilterVm { BatchId = id.Trim(), PageSize = AssetMovementService.MaxPageSize };
            var model = BuildMovementService().GetPage(filter);
            ViewBag.DepartmentGroups = BuildMoveTargetDepartmentGroups(null, null);
            ViewBag.MovementTypes = Enum.GetValues(typeof(AssetMovementType));
            return View("Index", model);
        }

        public ActionResult Export(AssetMovementFilterVm filter)
        {
            filter = filter ?? new AssetMovementFilterVm();
            return File(
                BuildMovementService().ExportCsv(filter),
                "text/csv",
                "asset-movements-" + KenyaTimeHelper.LocalDateStamp(DateTime.UtcNow) + ".csv");
        }
    }
}
