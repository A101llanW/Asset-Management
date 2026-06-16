using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.View")]
    public class MaintenanceController : BaseController
    {
        private readonly IMaintenanceService _maintenanceService;

        public MaintenanceController()
        {
            _maintenanceService = BuildMaintenanceService();
        }

        public ActionResult Details(int id)
        {
            var model = _maintenanceService.GetById(id);
            if (model == null)
            {
                return HttpNotFound();
            }

            ViewBag.CanComplete = HasPermission("Assets.Edit") && model.CanComplete;
            return View(model);
        }

        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Create(int assetId)
        {
            var model = new AssetMaintenanceVm
            {
                AssetId = assetId,
                MaintenanceType = MaintenanceType.Corrective.ToString()
            };

            PopulateCreateLookups(model);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Create([Bind(Prefix = "")] AssetMaintenanceVm viewModel)
        {
            if (viewModel == null)
            {
                return RedirectToAction("Index", "Assets");
            }

            PopulateCreateLookups(viewModel);
            ViewBag.AssetContext = BuildAssetWorkflowContext(viewModel.AssetId);
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                _maintenanceService.Create(viewModel);
                TempData["Message"] = "Maintenance ticket created.";
                return RedirectToAssetDetails(viewModel.AssetId);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(viewModel);
            }
        }

        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Complete(int id)
        {
            try
            {
                var model = _maintenanceService.GetCompleteModel(id);
                PopulateCompleteLookups(model);
                ViewBag.AssetContext = BuildAssetWorkflowContext(model.AssetId);
                return View(model);
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
                var record = _maintenanceService.GetById(id);
                if (record == null)
                {
                    return RedirectToAction("Index", "Assets");
                }

                return RedirectToAction("Details", new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Edit")]
        public ActionResult Complete([Bind(Prefix = "")] MaintenanceCompleteVm viewModel)
        {
            if (viewModel == null)
            {
                return RedirectToAction("Index", "Assets");
            }

            PopulateCompleteLookups(viewModel);
            ViewBag.AssetContext = BuildAssetWorkflowContext(viewModel.AssetId);
            if (string.Equals(viewModel.Disposition, MaintenanceDisposition.AssignToUser.ToString(), StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(viewModel.ToUserId)
                && !ValidateUserBelongsToDepartment(viewModel.ToUserId, viewModel.ToDepartmentId))
            {
                ModelState.AddModelError("ToUserId", "Selected user does not belong to the target department.");
            }

            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                _maintenanceService.Complete(viewModel);
                TempData["Message"] = "Maintenance ticket completed.";
                return RedirectToAssetDetails(viewModel.AssetId);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(viewModel);
            }
        }

        private void PopulateCreateLookups(AssetMaintenanceVm model)
        {
            var selectedType = string.IsNullOrWhiteSpace(model?.MaintenanceType)
                ? MaintenanceType.Corrective.ToString()
                : model.MaintenanceType;
            var maintenanceTypes = Enum.GetNames(typeof(MaintenanceType))
                .Select(x => new { Value = x, Text = x })
                .ToList();
            ViewBag.MaintenanceTypes = new SelectList(maintenanceTypes, "Value", "Text", selectedType);
        }

        private void PopulateCompleteLookups(MaintenanceCompleteVm model)
        {
            var activeUsers = GetActiveUsers().ToList();
            ViewBag.ConditionOptions = BuildAssetConditionSelectList(model?.ConditionAfter);
            ViewBag.Departments = BuildDepartmentSelectList(model?.ToDepartmentId);
            ViewBag.AllDepartments = BuildDepartmentSelectList(model?.ToDepartmentId);
            ViewBag.Users = BuildActiveUserSelectList(model?.ToUserId, model?.ToDepartmentId);

            SetWorkflowFormConfig(BuildWorkflowFormConfig(
                activeUsers,
                new[]
                {
                    new WorkflowDepartmentUserPairVm
                    {
                        DepartmentFieldId = "ToDepartmentId",
                        UserFieldId = "ToUserId"
                    }
                }));

            var dispositions = new List<SelectListItem>();
            if (!string.IsNullOrWhiteSpace(model?.PreviousOwnerUserId))
            {
                var label = string.IsNullOrWhiteSpace(model.PreviousOwnerName)
                    ? "Return to previous owner"
                    : "Return to previous owner (" + model.PreviousOwnerName + ")";
                dispositions.Add(new SelectListItem
                {
                    Value = MaintenanceDisposition.ReturnToPreviousOwner.ToString(),
                    Text = label
                });
            }

            dispositions.Add(new SelectListItem
            {
                Value = MaintenanceDisposition.KeepInStore.ToString(),
                Text = "Keep in store (available for assignment)"
            });
            dispositions.Add(new SelectListItem
            {
                Value = MaintenanceDisposition.AssignToUser.ToString(),
                Text = "Assign to a different user"
            });

            var selected = string.IsNullOrWhiteSpace(model?.Disposition)
                ? (dispositions.FirstOrDefault()?.Value ?? MaintenanceDisposition.KeepInStore.ToString())
                : model.Disposition;
            ViewBag.DispositionOptions = new SelectList(dispositions, "Value", "Text", selected);
            ViewBag.HasPreviousOwner = !string.IsNullOrWhiteSpace(model?.PreviousOwnerUserId);
        }

        private bool HasPermission(string permissionCode)
        {
            return BuildAuthorizationService().HasPermission(User.GetUserId(), permissionCode);
        }
    }
}
