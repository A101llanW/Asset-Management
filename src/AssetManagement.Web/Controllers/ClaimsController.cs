using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Claims.View")]
    public class ClaimsController : BaseController
    {
        private readonly IClaimService _claimService;

        public ClaimsController()
        {
            _claimService = BuildClaimService();
        }

        [PermissionAuthorize("Claims.Create")]
        public ActionResult Create(int assetId)
        {
            var model = new InsuranceClaimVm
            {
                AssetId = assetId,
                ClaimDate = System.DateTime.UtcNow,
                ClaimType = "Damage"
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Claims.Create")]
        public ActionResult Create(InsuranceClaimVm model)
        {
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(model.AssetId);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _claimService.Create(model);
                TempData["Message"] = "Claim initiated.";
                return RedirectToAction("Details", "Assets", new { id = model.AssetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private void PopulateLookups(InsuranceClaimVm model)
        {
            var claimTypes = new[]
            {
                "Damage",
                "Theft",
                "Loss",
                "Fire",
                "Accident",
                "Other"
            }.Select(x => new { Value = x, Text = x }).ToList();
            var selectedType = string.IsNullOrWhiteSpace(model?.ClaimType) ? "Damage" : model.ClaimType;
            ViewBag.ClaimTypes = new SelectList(claimTypes, "Value", "Text", selectedType);

            var incidents = model != null && model.AssetId > 0
                ? UnitOfWork.Repository<AssetIncident>().Find(x => x.AssetId == model.AssetId)
                    .OrderByDescending(x => x.IncidentDate)
                    .Select(x => new IncidentOptionVm
                    {
                        Id = x.Id,
                        Label = BuildIncidentLabel(x)
                    })
                    .ToList()
                : new List<IncidentOptionVm>();
            ViewBag.Incidents = new SelectList(incidents, "Id", "Label", model?.IncidentId);
        }

        private static string BuildIncidentLabel(AssetIncident incident)
        {
            var number = string.IsNullOrWhiteSpace(incident.IncidentNumber) ? ("INC-" + incident.Id) : incident.IncidentNumber;
            return number + " (" + incident.IncidentType + " - " + incident.IncidentDate.ToString("yyyy-MM-dd") + ")";
        }

        private class IncidentOptionVm
        {
            public int Id { get; set; }

            public string Label { get; set; }
        }
    }
}
