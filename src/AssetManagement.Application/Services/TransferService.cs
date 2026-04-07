using System;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class TransferService : ITransferService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;

        public TransferService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
        }

        public void Transfer(AssetTransferVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Transfer request is required.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                throw new BusinessException("Disposed or retired assets cannot be transferred.");
            }

            if (asset.CurrentStatus == AssetStatus.Lost || asset.CurrentStatus == AssetStatus.Stolen)
            {
                throw new BusinessException("Lost/stolen assets cannot be transferred unless recovered.");
            }

            if (asset.CurrentStatus != AssetStatus.Assigned)
            {
                throw new BusinessException("Only assigned assets can be transferred.");
            }

            var fromUserId = NormalizeId(model.FromUserId) ?? NormalizeId(asset.CurrentCustodianId);
            var toUserId = NormalizeId(model.ToUserId);
            var fromDepartmentId = model.FromDepartmentId ?? asset.DepartmentId;

            if (!string.IsNullOrWhiteSpace(model.FromUserId)
                && !string.IsNullOrWhiteSpace(asset.CurrentCustodianId)
                && !string.Equals(fromUserId, asset.CurrentCustodianId, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("From user does not match current custodian.");
            }

            var departmentChanged = model.ToDepartmentId.HasValue && model.ToDepartmentId.Value != fromDepartmentId;
            var custodianChanged = !string.IsNullOrWhiteSpace(toUserId) && !string.Equals(toUserId, fromUserId, StringComparison.OrdinalIgnoreCase);
            if (!departmentChanged && !custodianChanged)
            {
                throw new BusinessException("Transfer must change custodian or department.");
            }

            var transfer = new AssetTransfer
            {
                AssetId = model.AssetId,
                FromUserId = fromUserId,
                ToUserId = toUserId,
                FromDepartmentId = fromDepartmentId,
                ToDepartmentId = model.ToDepartmentId,
                Reason = NormalizeText(model.Reason),
                ConditionBefore = NormalizeText(model.ConditionBefore),
                ConditionAfter = NormalizeText(model.ConditionAfter),
                MissingAccessories = model.MissingAccessories,
                DamageNotes = NormalizeText(model.DamageNotes),
                ApprovalStatus = ApprovalStatus.Approved,
                TransferDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.Repository<AssetTransfer>().Add(transfer);
            _unitOfWork.Repository<AssetCustodyEvent>().Add(new AssetCustodyEvent
            {
                AssetId = model.AssetId,
                ActionType = CustodyActionType.Transferred,
                ActionDate = transfer.TransferDate,
                FromUserId = fromUserId,
                ToUserId = toUserId,
                FromDepartmentId = fromDepartmentId,
                ToDepartmentId = model.ToDepartmentId,
                ConditionBefore = NormalizeText(model.ConditionBefore),
                ConditionAfter = NormalizeText(model.ConditionAfter),
                Reason = NormalizeText(model.Reason),
                Notes = NormalizeText(model.DamageNotes),
                CreatedAt = DateTime.UtcNow
            });

            if (!string.IsNullOrWhiteSpace(toUserId))
            {
                asset.CurrentCustodianId = toUserId;
            }
            else if (model.ToDepartmentId.HasValue)
            {
                asset.CurrentCustodianId = null;
            }

            if (model.ToDepartmentId.HasValue)
            {
                asset.DepartmentId = model.ToDepartmentId.Value;
            }
            asset.CurrentStatus = AssetStatus.Assigned;
            asset.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(asset);

            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Transfer", nameof(AssetTransfer), transfer.Id.ToString(), null, transfer.AssetId.ToString());
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
