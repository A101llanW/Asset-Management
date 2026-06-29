using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Incidents.View")]
    public class IncidentsController : BaseController
    {
        private readonly IIncidentService _incidentService;

        public IncidentsController()
        {
            _incidentService = BuildIncidentService();
        }

        public ActionResult Index(string search, int? assetId, int page = 1, int pageSize = 10)
        {
            var items = _incidentService.GetIncidents(search, assetId);
            SetListSortViewBag(null, null);
            ViewBag.AssetId = assetId;
            return View(BuildListPage(items, search, null, null, page, pageSize));
        }

        public ActionResult Details(int id)
        {
            var model = _incidentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.CanEdit = HasPermission("Incidents.Edit");
            ViewBag.ResolutionStatuses = BuildResolutionStatusSelectList(model.ResolutionStatus);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Incidents.Edit")]
        public ActionResult UpdateStatus(int id, string resolutionStatus)
        {
            var model = _incidentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            try
            {
                _incidentService.UpdateResolutionStatus(id, resolutionStatus);
                TempData["Message"] = "Incident status updated.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
        }

        [PermissionAuthorize("Incidents.Create")]
        public ActionResult Create(int assetId)
        {
            var model = new AssetIncidentVm
            {
                AssetId = assetId,
                IncidentDate = DateTime.UtcNow
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Incidents.Create")]
        public ActionResult Create([Bind(Prefix = "")] AssetIncidentVm viewModel)
        {
            if (viewModel == null)
            {
                return RedirectToAction("Index", "Assets");
            }

            PopulateLookups(viewModel);
            ViewBag.AssetContext = BuildAssetWorkflowContext(viewModel.AssetId);
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                _incidentService.Create(viewModel);
                TempData["Message"] = "Incident reported.";
                return RedirectToAssetDetails(viewModel.AssetId);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(viewModel);
            }
        }

        private static SelectList BuildResolutionStatusSelectList(string selectedStatus)
        {
            var options = IncidentResolutionStatusHelper.GetSelectOptions(selectedStatus);
            return new SelectList(options, "Key", "Value", selectedStatus);
        }

        private void PopulateLookups(AssetIncidentVm model)
        {
            var selectedType = model?.IncidentType;
            var incidentTypes = Enum.GetNames(typeof(IncidentType))
                .Select(x => new { Value = x, Text = x })
                .ToList();
            ViewBag.IncidentTypes = new SelectList(incidentTypes, "Value", "Text", selectedType);

            var selectedSeverity = model?.Severity;
            var severities = Enum.GetNames(typeof(IncidentSeverity))
                .Select(x => new { Value = x, Text = x })
                .ToList();
            ViewBag.Severities = new SelectList(severities, "Value", "Text", selectedSeverity);
        }

        private bool HasPermission(string permissionCode)
        {
            return BuildAuthorizationService().HasPermission(User.GetUserId(), permissionCode);
        }
    }
}
