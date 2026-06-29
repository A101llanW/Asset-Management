using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Assets.Transfer")]
    public class TransfersController : BaseController
    {
        private readonly ITransferService _transferService;
        public TransfersController()
        {
            _transferService = BuildTransferService();
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

            if (!AssetCustodyRules.CanTransfer(asset.CurrentStatus))
            {
                TempData["Error"] = "Only assigned assets can be transferred.";
                return RedirectToAssetDetails(assetId);
            }

            var model = BuildTransferModel(asset);
            PopulateLookups(model, asset);
            ViewBag.AssetContext = BuildAssetWorkflowContext(assetId);
            ViewBag.TransferApprovalSummary = BuildAssetApprovalProcessSummary(asset, ApprovalProcessCodes.Transfer);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create([Bind(Prefix = "")] AssetTransferVm viewModel)
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

            ApplyTransferFieldRules(viewModel, asset);
            string scopeError;
            if (!EnsureAssetInCurrentUserDepartment(asset, out scopeError))
            {
                ModelState.AddModelError("", scopeError);
            }

            if (!string.IsNullOrWhiteSpace(viewModel.ToUserId) && !ValidateUserBelongsToDepartment(viewModel.ToUserId, viewModel.ToDepartmentId))
            {
                ModelState.AddModelError("ToUserId", "Selected user does not belong to the target department.");
            }

            PopulateLookups(viewModel, asset);
            ViewBag.AssetContext = BuildAssetWorkflowContext(viewModel.AssetId);
            ViewBag.TransferApprovalSummary = BuildAssetApprovalProcessSummary(asset, ApprovalProcessCodes.Transfer);
            if (!ModelState.IsValid)
            {
                return View(viewModel);
            }

            try
            {
                var result = _transferService.Transfer(viewModel, User.GetUserId());
                TempData["Message"] = result.RequiresApproval ? "Transfer request submitted for approval." : "Transfer recorded.";
                TempData["Guidance"] = result.RequiresApproval
                    ? "Next step: the configured approver role can review this request from the asset details page."
                    : null;
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
        [PermissionAuthorize("Assets.Transfer")]
        public ActionResult Approve(int transferId, int assetId, string notes)
        {
            try
            {
                _transferService.ApproveTransfer(new TransferApprovalDecisionVm
                {
                    TransferId = transferId,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Transfer approval recorded.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetails(assetId);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [PermissionAuthorize("Assets.Transfer")]
        public ActionResult Reject(int transferId, int assetId, string notes)
        {
            try
            {
                _transferService.RejectTransfer(new TransferApprovalDecisionVm
                {
                    TransferId = transferId,
                    Notes = notes
                }, User.GetUserId(), GetCurrentUserRoleId(), IsCurrentUserSuperAdmin());
                TempData["Message"] = "Transfer request rejected.";
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAssetDetails(assetId);
        }

        private static AssetTransferVm BuildTransferModel(Asset asset)
        {
            return new AssetTransferVm
            {
                AssetId = asset.Id,
                FromUserId = asset.CurrentCustodianId,
                FromDepartmentId = asset.DepartmentId > 0 ? (int?)asset.DepartmentId : null
            };
        }

        private void ApplyTransferFieldRules(AssetTransferVm model, Asset asset)
        {
            if (model == null || asset == null)
            {
                return;
            }

            model.FromUserId = asset.CurrentCustodianId;
            model.FromDepartmentId = asset.DepartmentId > 0 ? (int?)asset.DepartmentId : null;

            ApplyLockedUserDepartment(GetCurrentUserDepartmentId(), deptId =>
            {
                if (!deptId.HasValue)
                {
                    return;
                }

                model.ToDepartmentId = deptId;
            });
        }

        private void PopulateLookups(AssetTransferVm model, Asset asset)
        {
            var activeUsers = GetActiveUsers().ToList();
            var departments = BuildDepartmentService().GetAll().Where(x => x.IsActive).OrderBy(x => x.Name).ToList();
            var lockToDepartment = !IsCurrentUserSuperAdmin() && GetCurrentUserDepartmentId().HasValue;
            var toDepartmentId = model?.ToDepartmentId ?? (lockToDepartment ? GetCurrentUserDepartmentId() : null);

            if (lockToDepartment && !toDepartmentId.HasValue)
            {
                toDepartmentId = GetCurrentUserDepartmentId();
                if (model != null)
                {
                    model.ToDepartmentId = toDepartmentId;
                }
            }

            ViewBag.Users = BuildActiveUserSelectList(model?.ToUserId, toDepartmentId);
            ViewBag.FromUsers = BuildActiveUserSelectList(model?.FromUserId);
            ViewBag.Departments = BuildDepartmentSelectList(toDepartmentId);
            ViewBag.AllDepartments = BuildDepartmentSelectList(model?.ToDepartmentId);
            ViewBag.LockToDepartment = lockToDepartment;
            ViewBag.LockFromFields = true;
            ViewBag.FromDepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(model?.FromDepartmentId, departments);
            ViewBag.FromUserName = DepartmentUserWorkflowHelper.ResolveUserDisplayName(model?.FromUserId, activeUsers);
            ViewBag.ToDepartmentName = DepartmentUserWorkflowHelper.ResolveDepartmentDisplayName(toDepartmentId, departments);

            var lockedFields = new List<WorkflowLockedFieldVm>
            {
                new WorkflowLockedFieldVm { FieldId = "FromUserId" },
                new WorkflowLockedFieldVm { FieldId = "FromDepartmentId" }
            };
            if (lockToDepartment)
            {
                lockedFields.Add(new WorkflowLockedFieldVm { FieldId = "ToDepartmentId" });
            }

            SetWorkflowFormConfig(BuildWorkflowFormConfig(
                activeUsers,
                new[]
                {
                    new WorkflowDepartmentUserPairVm
                    {
                        DepartmentFieldId = "ToDepartmentId",
                        UserFieldId = "ToUserId",
                        RequireDepartmentForUsers = true
                    }
                },
                lockedFields));
        }
    }
}
