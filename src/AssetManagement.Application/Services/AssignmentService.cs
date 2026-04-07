using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class AssignmentService : IAssignmentService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;

        public AssignmentService(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
        }

        public IEnumerable<AssetAssignmentVm> GetByAsset(int assetId)
        {
            return _unitOfWork.Repository<AssetAssignment>().Find(x => x.AssetId == assetId)
                .OrderByDescending(x => x.AssignedDate)
                .Select(x => new AssetAssignmentVm
                {
                    AssetId = x.AssetId,
                    ToDepartmentId = x.ToDepartmentId,
                    ToUserId = x.ToUserId,
                    AssignmentType = x.AssignmentType.ToString(),
                    AssignedDate = x.AssignedDate,
                    ExpectedReturnDate = x.ExpectedReturnDate,
                    ConditionBeforeHandover = x.ConditionBeforeHandover,
                    AccessoriesHandedOver = x.AccessoriesHandedOver,
                    HandoverNotes = x.HandoverNotes,
                    HandedOverById = x.HandedOverById,
                    ReceivedById = x.ReceivedById
                })
                .ToList();
        }

        public void Assign(AssetAssignmentVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Assignment request is required.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            if (asset.CurrentStatus == AssetStatus.Disposed)
            {
                throw new BusinessException("Disposed assets cannot be assigned.");
            }

            if (asset.CurrentStatus == AssetStatus.Lost || asset.CurrentStatus == AssetStatus.Stolen)
            {
                throw new BusinessException("Lost or stolen assets cannot be assigned.");
            }

            var toUserId = NormalizeId(model.ToUserId);
            var handedOverById = NormalizeId(model.HandedOverById);
            var receivedById = NormalizeId(model.ReceivedById);
            var assignedDate = model.AssignedDate == default(DateTime) ? DateTime.UtcNow : model.AssignedDate;
            var type = ParseAssignmentType(model.AssignmentType);
            if (string.IsNullOrWhiteSpace(toUserId) && !model.ToDepartmentId.HasValue)
            {
                throw new BusinessException("Assignment requires a target user or department.");
            }

            if (type == AssignmentType.Temporary && !model.ExpectedReturnDate.HasValue)
            {
                throw new BusinessException("Temporary assignments must have expected return date.");
            }

            if (type == AssignmentType.Temporary && model.ExpectedReturnDate <= assignedDate)
            {
                throw new BusinessException("Expected return date must be later than assignment date.");
            }

            if (type == AssignmentType.DepartmentPool && !model.ToDepartmentId.HasValue)
            {
                throw new BusinessException("Department pool assignment requires a target department.");
            }

            if (!string.IsNullOrWhiteSpace(toUserId)
                && string.Equals(asset.CurrentCustodianId, toUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Asset is already assigned to the selected user.");
            }

            var assignment = new AssetAssignment
            {
                AssetId = model.AssetId,
                ToDepartmentId = model.ToDepartmentId,
                ToUserId = toUserId,
                AssignmentType = type,
                AssignedDate = assignedDate,
                ExpectedReturnDate = model.ExpectedReturnDate,
                ConditionBeforeHandover = NormalizeText(model.ConditionBeforeHandover),
                AccessoriesHandedOver = NormalizeText(model.AccessoriesHandedOver),
                HandoverNotes = NormalizeText(model.HandoverNotes),
                HandedOverById = handedOverById,
                ReceivedById = receivedById,
                RecipientAcknowledged = false,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.Repository<AssetAssignment>().Add(assignment);

            var custody = new AssetCustodyEvent
            {
                AssetId = model.AssetId,
                ActionType = CustodyActionType.Assigned,
                ActionDate = assignment.AssignedDate,
                FromUserId = asset.CurrentCustodianId,
                ToUserId = toUserId,
                FromDepartmentId = asset.DepartmentId,
                ToDepartmentId = model.ToDepartmentId,
                ConditionBefore = NormalizeText(model.ConditionBeforeHandover),
                ConditionAfter = NormalizeText(model.ConditionBeforeHandover),
                Notes = NormalizeText(model.HandoverNotes),
                CreatedAt = DateTime.UtcNow
            };
            _unitOfWork.Repository<AssetCustodyEvent>().Add(custody);

            asset.CurrentCustodianId = toUserId;
            if (model.ToDepartmentId.HasValue)
            {
                asset.DepartmentId = model.ToDepartmentId.Value;
            }
            asset.CurrentStatus = AssetStatus.Assigned;
            asset.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(asset);

            _unitOfWork.SaveChanges();
            _auditWriter.Write("Assets.Assign", nameof(AssetAssignment), assignment.Id.ToString(), null, assignment.AssetId.ToString());
        }

        private static AssignmentType ParseAssignmentType(string assignmentType)
        {
            if (string.IsNullOrWhiteSpace(assignmentType))
            {
                return AssignmentType.Permanent;
            }

            AssignmentType parsed;
            if (!Enum.TryParse(assignmentType, true, out parsed))
            {
                throw new BusinessException("Invalid assignment type.");
            }

            return parsed;
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
