using System;
using System.Linq;
using System.Web;
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
        private readonly IAssetDocumentRequirementService _documentRequirementService;
        private readonly IAssetDocumentService _documentService;

        public IncidentsController()
        {
            _incidentService = BuildIncidentService();
            _documentRequirementService = BuildAssetDocumentRequirementService();
            _documentService = BuildAssetDocumentService();
        }

        public ActionResult Index(string search, int? assetId, int page = 1, int pageSize = 10)
        {
            var pageResult = _incidentService.GetListPage(search, assetId, page, pageSize);
            SetListSortViewBag(null, null);
            ViewBag.AssetId = assetId;
            return View(ToListPage(pageResult));
        }

        public ActionResult Details(int id)
        {
            var model = _incidentService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            IncidentType incidentType;
            if (Enum.TryParse(model.IncidentType, true, out incidentType)
                && AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(incidentType))
            {
                var pendingRequirement = _documentRequirementService.GetPendingIncidentPhotoRequirement(id);
                if (pendingRequirement != null)
                {
                    model.RequiresDamagePhoto = true;
                    model.PendingPhotoRequirementId = pendingRequirement.Id;
                }
            }

            ViewBag.CanEdit = HasPermission("Incidents.Edit");
            ViewBag.CanUploadPhoto = HtmlCanUploadDocument(model.AssetId);
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
        public ActionResult Create(int? assetId)
        {
            if (!assetId.HasValue)
            {
                TempData["Error"] = "Select an asset to report an incident for.";
                return RedirectToAction("Index", "Assets");
            }

            var model = new AssetIncidentVm
            {
                AssetId = assetId.Value,
                IncidentDate = DateTime.UtcNow
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId.Value);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Incidents.Create")]
        public ActionResult Create([Bind(Prefix = "")] AssetIncidentVm viewModel, HttpPostedFileBase damagePhoto)
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
                var incidentId = _incidentService.Create(viewModel);
                var incident = _incidentService.GetById(incidentId);
                IncidentType incidentType;
                if (incident != null
                    && Enum.TryParse(incident.IncidentType, true, out incidentType)
                    && AssetDocumentProcessHelper.IncidentTypeRequiresPhoto(incidentType))
                {
                    var requirementId = _documentRequirementService.CreateIncidentPhotoRequirement(
                        viewModel.AssetId,
                        incidentId,
                        incident.IncidentNumber);

                    if (damagePhoto != null && damagePhoto.ContentLength > 0)
                    {
                        using (var stream = damagePhoto.InputStream)
                        {
                            _documentService.UploadForRequirement(
                                viewModel.AssetId,
                                requirementId,
                                damagePhoto.FileName,
                                damagePhoto.ContentType,
                                stream,
                                User.GetUserId());
                        }

                        TempData["Message"] = "Incident reported and damage photo linked to the asset.";
                        return RedirectToAssetDetails(viewModel.AssetId);
                    }

                    TempData["Message"] = "Incident reported. Upload a damage photo from the asset Documents tab.";
                    return Redirect(Url.Action("Details", "Assets", new { id = viewModel.AssetId, uploadRequirement = requirementId }) + "#documents");
                }

                TempData["Message"] = "Incident reported.";
                return RedirectToAssetDetails(viewModel.AssetId);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(viewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UploadDamagePhoto(int id, HttpPostedFileBase damagePhoto)
        {
            var incident = _incidentService.GetById(id);
            if (incident == null)
            {
                return HttpNotFound();
            }

            var pendingRequirement = _documentRequirementService.GetPendingIncidentPhotoRequirement(id);
            if (pendingRequirement == null)
            {
                TempData["Error"] = "No pending damage photo is required for this incident.";
                return RedirectToAction("Details", new { id });
            }

            if (damagePhoto == null || damagePhoto.ContentLength == 0)
            {
                TempData["Error"] = "Select a photo to upload.";
                return RedirectToAction("Details", new { id });
            }

            try
            {
                using (var stream = damagePhoto.InputStream)
                {
                    _documentService.UploadForRequirement(
                        incident.AssetId,
                        pendingRequirement.Id,
                        damagePhoto.FileName,
                        damagePhoto.ContentType,
                        stream,
                        User.GetUserId());
                }

                TempData["Message"] = "Damage photo uploaded and linked to the incident.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { id });
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

        private bool HtmlCanUploadDocument(int assetId)
        {
            var asset = BuildAssetService().GetById(assetId);
            if (asset == null)
            {
                return false;
            }

            var userId = User.GetUserId();
            if (!string.IsNullOrWhiteSpace(userId)
                && !string.IsNullOrWhiteSpace(asset.CurrentCustodianId)
                && string.Equals(asset.CurrentCustodianId, userId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return HasPermission("Documents.Upload");
        }
    }
}
