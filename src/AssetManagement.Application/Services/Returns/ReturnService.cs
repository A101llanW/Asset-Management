using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class ReturnService : IReturnService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IUserService _userService;
        private readonly IAssetWorkflowGuard _workflowGuard;

        public ReturnService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IUserService userService,
            IAssetWorkflowGuard workflowGuard)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _userService = userService;
            _workflowGuard = workflowGuard;
        }

        public void ReturnAsset(AssetReturnVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Return request is required.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                throw new BusinessException("Disposed or retired assets cannot be returned.");
            }

            if (asset.CurrentStatus != AssetStatus.Assigned && asset.CurrentStatus != AssetStatus.UnderMaintenance)
            {
                throw new BusinessException("Only assigned or under-maintenance assets can be returned.");
            }

            _workflowGuard.EnsureNoBlockingWorkflow(model.AssetId);

            var returnedById = NormalizeId(model.ReturnedById) ?? NormalizeId(asset.CurrentCustodianId);
            var receivedById = NormalizeId(model.ReceivedById);
            if (string.IsNullOrWhiteSpace(returnedById))
            {
                throw new BusinessException("A returning user is required.");
            }

            if (string.IsNullOrWhiteSpace(receivedById))
            {
                throw new BusinessException("A receiving user is required.");
            }

            if (!string.IsNullOrWhiteSpace(asset.CurrentCustodianId)
                && !string.IsNullOrWhiteSpace(model.ReturnedById)
                && !string.Equals(returnedById, asset.CurrentCustodianId, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Returned by user does not match current custodian.");
            }

            if (asset.DepartmentId.HasValue && asset.DepartmentId.Value > 0)
            {
                EnsureUserBelongsToDepartment(receivedById, asset.DepartmentId.Value);
            }

            var returnDate = model.ReturnDate == default(DateTime) ? DateTime.UtcNow : model.ReturnDate;
            var returnRecord = new AssetReturn
            {
                AssetId = model.AssetId,
                ReturnedById = returnedById,
                ReceivedById = receivedById,
                ReturnDate = returnDate,
                ReturnCondition = NormalizeText(model.ReturnCondition),
                MissingAccessories = model.MissingAccessories,
                DamageNotes = NormalizeText(model.DamageNotes),
                Notes = NormalizeText(model.Notes),
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.Repository<AssetReturn>().Add(returnRecord);
            _unitOfWork.Repository<AssetCustodyEvent>().Add(new AssetCustodyEvent
            {
                AssetId = model.AssetId,
                ActionType = CustodyActionType.Returned,
                ActionDate = returnRecord.ReturnDate,
                FromUserId = returnedById,
                FromDepartmentId = asset.DepartmentId,
                ToDepartmentId = asset.DepartmentId,
                ConditionAfter = NormalizeText(model.ReturnCondition),
                Notes = NormalizeText(model.Notes),
                CreatedAt = DateTime.UtcNow
            });

            asset.CurrentCustodianId = null;
            asset.CurrentStatus = AssetStatus.Returned;
            asset.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(asset);

            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Return", nameof(AssetReturn), returnRecord.Id.ToString(), null, returnRecord.AssetId.ToString());
        }

        private void EnsureUserBelongsToDepartment(string userId, int departmentId)
        {
            var user = _userService.GetById(userId);
            if (user == null || !user.IsActive)
            {
                throw new BusinessException("Selected user was not found or is inactive.");
            }

            if (!user.DepartmentId.HasValue || user.DepartmentId.Value != departmentId)
            {
                throw new BusinessException("Receiving user must belong to the asset's department.");
            }
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
