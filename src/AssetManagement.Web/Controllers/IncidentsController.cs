using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;

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

        [PermissionAuthorize("Incidents.Create")]
        public ActionResult Create(int assetId)
        {
            var model = new AssetIncidentVm
            {
                AssetId = assetId,
                IncidentDate = DateTime.UtcNow,
                IncidentType = IncidentType.Damaged.ToString()
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Incidents.Create")]
        public ActionResult Create(AssetIncidentVm model)
        {
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(model.AssetId);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _incidentService.Create(model);
                TempData["Message"] = "Incident reported.";
                return RedirectToAction("Details", "Assets", new { id = model.AssetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private void PopulateLookups(AssetIncidentVm model)
        {
            var selectedType = string.IsNullOrWhiteSpace(model?.IncidentType)
                ? IncidentType.Damaged.ToString()
                : model.IncidentType;
            var incidentTypes = Enum.GetNames(typeof(IncidentType))
                .Select(x => new { Value = x, Text = x })
                .ToList();
            ViewBag.IncidentTypes = new SelectList(incidentTypes, "Value", "Text", selectedType);
        }
    }
}
