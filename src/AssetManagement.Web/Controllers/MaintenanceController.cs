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
    [PermissionAuthorize("Assets.Edit")]
    public class MaintenanceController : BaseController
    {
        private readonly IMaintenanceService _maintenanceService;

        public MaintenanceController()
        {
            _maintenanceService = BuildMaintenanceService();
        }

        public ActionResult Create(int assetId)
        {
            var model = new AssetMaintenanceVm
            {
                AssetId = assetId,
                MaintenanceType = MaintenanceType.Corrective.ToString()
            };

            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(AssetMaintenanceVm model)
        {
            PopulateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(model.AssetId);
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                _maintenanceService.Create(model);
                TempData["Message"] = "Maintenance ticket created.";
                return RedirectToAction("Details", "Assets", new { id = model.AssetId });
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(model);
            }
        }

        private void PopulateLookups(AssetMaintenanceVm model)
        {
            var selectedType = string.IsNullOrWhiteSpace(model?.MaintenanceType)
                ? MaintenanceType.Corrective.ToString()
                : model.MaintenanceType;
            var maintenanceTypes = Enum.GetNames(typeof(MaintenanceType))
                .Select(x => new { Value = x, Text = x })
                .ToList();
            ViewBag.MaintenanceTypes = new SelectList(maintenanceTypes, "Value", "Text", selectedType);
        }
    }
}
