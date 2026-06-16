using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Return")]
    public class ReturnsController : BaseController
    {
        private readonly IReturnService _returnService;
        public ReturnsController()
        {
            _returnService = BuildReturnService();
        }

        public ActionResult Wizard(int assetId)
        {
            var result = Create(assetId);
            if (result is ViewResult)
            {
                return View("Wizard", ((ViewResult)result).Model);
            }

            return result;
        }

        public ActionResult Create(int assetId)
        {
            var asset = UnitOfWork.Repository<Asset>().GetById(assetId);
            if (asset == null)
            {
                return HttpNotFound();
            }

            string scopeError;
            if (!EnsureAssetInCurrentUserDepartment(asset, out scopeError))
            {
                TempData["Error"] = scopeError;
                return RedirectToAssetDetails(assetId);
            }

            var model = new AssetReturnVm
            {
                AssetId = assetId,
                ReturnedById = asset.CurrentCustodianId,
                ReturnDate = DateTime.UtcNow
            };

            PopulateLookups(model, asset);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Prefix = "")] AssetReturnVm viewModel)
        {
            if (viewModel == null)
            {
                return RedirectToAction("Index", "Assets");
            }

            var asset = UnitOfWork.Repository<Asset>().GetById(viewModel.AssetId);
            if (asset == null)
            {
                return HttpNotFound();
            }

            viewModel.ReturnedById = asset.CurrentCustodianId;
            string scopeError;
            if (!EnsureAssetInCurrentUserDepartment(asset, out scopeError))
            {
                ModelState.AddModelError("", scopeError);
            }

            var receiveDepartmentId = asset.DepartmentId > 0 ? (int?)asset.DepartmentId : null;
            if (!ValidateUserBelongsToDepartment(viewModel.ReceivedById, receiveDepartmentId))
            {
                ModelState.AddModelError("ReceivedById", "Receiving user must belong to the asset's department.");
            }

            PopulateLookups(viewModel, asset);
            ViewBag.AssetContext = BuildAssetWorkflowContext(viewModel.AssetId);
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                _returnService.ReturnAsset(viewModel);
                TempData["Message"] = "Return logged.";
                return RedirectToAssetDetails(viewModel.AssetId);
            }
            catch (BusinessException ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(viewModel);
            }
        }

        private void PopulateLookups(AssetReturnVm model, Asset asset)
        {
            var activeUsers = GetActiveUsers().ToList();
            var receiveDepartmentId = asset != null && asset.DepartmentId > 0 ? (int?)asset.DepartmentId : null;
            ViewBag.Users = BuildActiveUserSelectList(model?.ReceivedById, receiveDepartmentId);
            ViewBag.LockReturnedBy = !string.IsNullOrWhiteSpace(asset?.CurrentCustodianId);
            ViewBag.ReturnedByName = DepartmentUserWorkflowHelper.ResolveUserDisplayName(model?.ReturnedById, activeUsers);
            ViewBag.ReceiveDepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(
                receiveDepartmentId,
                BuildDepartmentService().GetAll().Where(x => x.IsActive).ToList());

            SetWorkflowFormConfig(BuildWorkflowFormConfig(
                activeUsers,
                new WorkflowDepartmentUserPairVm[0],
                ViewBag.LockReturnedBy == true
                    ? new[] { new WorkflowLockedFieldVm { FieldId = "ReturnedById" } }
                    : new WorkflowLockedFieldVm[0]));
        }
    }
}
