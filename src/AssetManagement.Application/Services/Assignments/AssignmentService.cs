using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
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
        private readonly IUserService _userService;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IAssetWorkflowGuard _workflowGuard;
        private readonly IOperationsQueryRepository _operationsQueryRepository;
        private readonly IOrganizationScopeService _organizationScope;

        public AssignmentService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IUserService userService,
            IDepartmentScopeService departmentScope,
            IAssetWorkflowGuard workflowGuard,
            IOperationsQueryRepository operationsQueryRepository,
            IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _userService = userService;
            _departmentScope = departmentScope;
            _workflowGuard = workflowGuard;
            _operationsQueryRepository = operationsQueryRepository;
            _organizationScope = organizationScope;
        }

        public AssignmentListPageVm GetAssignmentListPage(AssignmentFilterVm filter, string sort, string direction, int page, int pageSize)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new AssignmentListPageVm
                {
                    Items = new List<AssignmentListVm>(),
                    TotalCount = 0,
                    Page = 1,
                    PageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100)
                };
            }

            var bypassDepartmentScope = _departmentScope.BypassesDepartmentScope;
            int? departmentId = null;
            var denyDepartmentScope = false;
            if (!bypassDepartmentScope)
            {
                departmentId = _departmentScope.ScopedDepartmentId;
                denyDepartmentScope = !departmentId.HasValue;
            }

            return _operationsQueryRepository.GetAssignmentListPage(
                filter,
                sort,
                direction,
                page,
                pageSize,
                organizationId.Value,
                departmentId,
                bypassDepartmentScope,
                denyDepartmentScope);
        }

        public IEnumerable<AssetAssignmentVm> GetByAsset(int assetId)
        {
            var asset = _unitOfWork.Repository<Asset>().GetById(assetId);
            if (asset != null)
            {
                _departmentScope.EnsureCanAccessAsset(asset);
            }

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
            AssetAssignment assignment = null;
            _unitOfWork.ExecuteInTransaction(() => { assignment = AssignWithoutSave(model); });
            _auditWriter.Write("Assets.Assign", nameof(AssetAssignment), assignment.Id.ToString(), null, model.AssetId.ToString());
        }

        public AssetAssignment AssignWithoutSave(AssetAssignmentVm model)
        {
            if (model == null)
            {
                throw new BusinessException("Assignment request is required.");
            }

            _workflowGuard.EnsureNoBlockingWorkflow(model.AssetId);

            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            _departmentScope.EnsureCanAccessAsset(asset);

            if (asset.CurrentStatus == AssetStatus.Disposed)
            {
                throw new BusinessException("Disposed assets cannot be assigned.");
            }

            if (asset.CurrentStatus == AssetStatus.Lost || asset.CurrentStatus == AssetStatus.Stolen)
            {
                throw new BusinessException("Lost or stolen assets cannot be assigned.");
            }

            if (!AssetCustodyRules.CanAssign(asset.CurrentStatus))
            {
                throw new BusinessException(AssetCustodyRules.AlreadyAssignedMessage);
            }

            ValidateConditionBeforeHandover(model.ConditionBeforeHandover);

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

            EnsureUserBelongsToDepartment(toUserId, model.ToDepartmentId);
            EnsureUserBelongsToDepartment(receivedById, model.ToDepartmentId);

            var assignment = new AssetAssignment
            {
                AssetId = model.AssetId,
                ToDepartmentId = model.ToDepartmentId,
                ToUserId = toUserId,
                AssignmentType = type,
                AssignedDate = assignedDate,
                ExpectedReturnDate = type == AssignmentType.Temporary ? model.ExpectedReturnDate : null,
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
            return assignment;
        }

        private void EnsureUserBelongsToDepartment(string userId, int? departmentId)
        {
            if (string.IsNullOrWhiteSpace(userId) || !departmentId.HasValue)
            {
                return;
            }

            var user = _userService.GetById(userId);
            if (user == null || !user.IsActive)
            {
                throw new BusinessException("Selected user was not found or is inactive.");
            }

            if (!user.DepartmentId.HasValue || user.DepartmentId.Value != departmentId.Value)
            {
                throw new BusinessException("Selected user does not belong to the target department.");
            }
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

        private static void ValidateConditionBeforeHandover(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return;
            }

            AssetCondition parsed;
            if (!Enum.TryParse(condition, true, out parsed) || !Enum.IsDefined(typeof(AssetCondition), parsed))
            {
                throw new BusinessException("Select a valid condition before handover.");
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
