using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Claims.View")]
    public class ClaimsController : BaseController
    {
        private static readonly string[] StandardClaimTypes =
        {
            "Damage",
            "Theft",
            "Loss",
            "Fire",
            "Accident",
            "Other"
        };

        private readonly IClaimService _claimService;

        public ClaimsController()
        {
            _claimService = BuildClaimService();
        }

        public ActionResult Index(string search, int? assetId, int page = 1, int pageSize = 10)
        {
            var items = _claimService.GetClaims(search, assetId);
            ViewBag.AssetId = assetId;
            return View(BuildListPage(items, search, null, null, page, pageSize));
        }

        public ActionResult Details(int id)
        {
            var model = _claimService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.CanEdit = HasPermission("Claims.Edit");
            ViewBag.ClaimStatuses = BuildClaimStatusSelectList(model.ClaimStatus);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Claims.Edit")]
        public ActionResult UpdateStatus(int id, ClaimStatus status, decimal? approvedAmount, string settlementNotes)
        {
            var model = _claimService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            try
            {
                _claimService.UpdateStatus(id, status, approvedAmount, settlementNotes);
                TempData["Message"] = "Claim status updated.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
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

        private static SelectList BuildClaimStatusSelectList(ClaimStatus selectedStatus)
        {
            var options = new[]
            {
                ClaimStatus.Submitted,
                ClaimStatus.UnderReview,
                ClaimStatus.Approved,
                ClaimStatus.Settled,
                ClaimStatus.Rejected
            }.Select(x => new { Value = ((int)x).ToString(), Text = x.ToString() });
            return new SelectList(options, "Value", "Text", ((int)selectedStatus).ToString());
        }

        private bool HasPermission(string permissionCode)
        {
            return BuildAuthorizationService().HasPermission(User.GetUserId(), permissionCode);
        }

        private void PopulateLookups(InsuranceClaimVm model)
        {
            var claimTypes = StandardClaimTypes.Select(x => new { Value = x, Text = x }).ToList();
            var postedType = model == null ? null : model.ClaimType;
            var selectedType = "Damage";
            var otherClaimType = string.Empty;

            if (!string.IsNullOrWhiteSpace(postedType))
            {
                if (StandardClaimTypes.Any(x => string.Equals(x, postedType, StringComparison.OrdinalIgnoreCase)))
                {
                    selectedType = StandardClaimTypes.First(x => string.Equals(x, postedType, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    selectedType = "Other";
                    otherClaimType = postedType.Trim();
                }
            }

            ViewBag.ClaimTypes = new SelectList(claimTypes, "Value", "Text", selectedType);
            ViewBag.OtherClaimType = otherClaimType;

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
