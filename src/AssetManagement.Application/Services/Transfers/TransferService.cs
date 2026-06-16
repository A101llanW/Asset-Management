using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
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
        private readonly IUserService _userService;
        private readonly IDepartmentScopeService _departmentScope;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOutboxWriter _outboxWriter;
        private readonly IWebhookService _webhookService;
        private readonly IAssetWorkflowGuard _workflowGuard;
        private readonly IApprovalWorkflowEngine _approvalEngine;

        public TransferService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IUserService userService,
            IDepartmentScopeService departmentScope,
            IOrganizationScopeService organizationScope,
            IOutboxWriter outboxWriter,
            IWebhookService webhookService,
            IAssetWorkflowGuard workflowGuard,
            IApprovalWorkflowEngine approvalEngine)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _userService = userService;
            _departmentScope = departmentScope;
            _organizationScope = organizationScope;
            _outboxWriter = outboxWriter;
            _webhookService = webhookService;
            _workflowGuard = workflowGuard;
            _approvalEngine = approvalEngine;
        }

        public TransferSubmissionResultVm Transfer(AssetTransferVm model, string requestedByUserId)
        {
            if (model == null)
            {
                throw new BusinessException("Transfer request is required.");
            }

            if (string.IsNullOrWhiteSpace(requestedByUserId))
            {
                throw new BusinessException("Current user is required for transfer request.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(model.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            _departmentScope.EnsureCanAccessAsset(asset);
            _workflowGuard.EnsureNoBlockingWorkflow(model.AssetId);
            EnsureTransferIsAllowed(asset, model);
            EnsureTransferFieldIntegrity(asset, model);

            var hasPending = _unitOfWork.Repository<AssetTransfer>()
                .Find(x => x.AssetId == model.AssetId && x.ApprovalStatus == ApprovalStatus.Pending && x.IsActive)
                .Any();
            if (hasPending)
            {
                throw new BusinessException("A transfer request is already pending approval for this asset.");
            }

            var transfer = CreateTransferRecord(asset, model, requestedByUserId);
            var approvalConfig = ApprovalWorkflowHelper.GetAssetProcessConfiguration(_unitOfWork, asset, ApprovalProcessCodes.Transfer);

            if (!approvalConfig.UsesApproval)
            {
                transfer.ApprovalStatus = ApprovalStatus.Approved;
                transfer.ApprovedById = requestedByUserId;
                transfer.CurrentApprovalStage = 0;
                _unitOfWork.ExecuteInTransaction(() =>
                {
                    _unitOfWork.Repository<AssetTransfer>().Add(transfer);
                    _unitOfWork.SaveChanges();
                    ApplyTransfer(asset, transfer);
                    NotificationHelper.AddNotification(
                        _unitOfWork,
                        _outboxWriter,
                        _organizationScope,
                        requestedByUserId,
                        NotificationType.General,
                        "Transfer completed",
                        "Transfer #" + transfer.Id + " for asset " + asset.AssetTag + " was completed immediately.",
                        "/Assets/Details/" + transfer.AssetId);
                });
                _auditWriter.Write("Assets.Transfer", nameof(AssetTransfer), transfer.Id.ToString(), null, transfer.AssetId.ToString());
                return new TransferSubmissionResultVm
                {
                    TransferId = transfer.Id,
                    RequiresApproval = false
                };
            }

            transfer.ApprovalStatus = ApprovalStatus.Pending;
            transfer.CurrentApprovalStage = 1;
            transfer.ApprovalStageRoleIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(approvalConfig.StageRoleIds.Select(x => (int?)x));
            transfer.ApprovalStageUserIds = ApprovalWorkflowSettingsHelper.SerializeStageUserIds(approvalConfig.StageUserIds);
            transfer.OriginalAssetStatus = asset.CurrentStatus;

            var oldStatus = asset.CurrentStatus;
            asset.CurrentStatus = AssetStatus.AwaitingApproval;
            asset.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.ExecuteInTransaction(() =>
            {
                _unitOfWork.Repository<AssetTransfer>().Add(transfer);
                _unitOfWork.SaveChanges();
                _unitOfWork.Repository<Asset>().Update(asset);
                NotificationHelper.AddNotification(
                    _unitOfWork,
                    _outboxWriter,
                    _organizationScope,
                    requestedByUserId,
                    NotificationType.PendingApproval,
                    "Transfer submitted",
                    "Transfer #" + transfer.Id + " for asset " + asset.AssetTag + " is pending approval.",
                    "/Assets/Details/" + transfer.AssetId);
                NotificationHelper.AddStageApproverNotification(
                    _unitOfWork,
                    _outboxWriter,
                    _organizationScope,
                    _userService,
                    approvalConfig.StageRoleIds.FirstOrDefault(),
                    ApprovalWorkflowHelper.GetStageUserId(approvalConfig.StageUserIds, 1),
                    "Transfer approval required",
                    "Transfer #" + transfer.Id + " for asset " + asset.AssetTag + " is awaiting Stage 1 approval.",
                    "/Assets/Details/" + transfer.AssetId);
            });

            _auditWriter.Write("Assets.Transfer", nameof(AssetTransfer), transfer.Id.ToString(), oldStatus.ToString(), "PendingApproval");
            return new TransferSubmissionResultVm
            {
                TransferId = transfer.Id,
                RequiresApproval = true
            };
        }

        public void ApproveTransfer(TransferApprovalDecisionVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            if (model == null)
            {
                throw new BusinessException("Transfer approval payload is required.");
            }

            var transfer = GetPendingTransfer(model.TransferId);
            EnsureCanAccessTransferAsset(transfer);
            var asset = _unitOfWork.Repository<Asset>().GetById(transfer.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(transfer.ApprovalStageRoleIds);
            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(transfer.ApprovalStageUserIds);
            var stageNumber = transfer.CurrentApprovalStage <= 0 ? 1 : transfer.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Transfer);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);
            var isFinalStage = stageNumber >= stageRoleIds.Count;

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Transfer,
                ActingUserId = approvedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = transfer.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Approved,
                RequestEntity = transfer,
                RequestEntityType = typeof(AssetTransfer),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApprovalActionEntity = new TransferApprovalAction
                {
                    AssetTransferId = transfer.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = approvedByUserId,
                    Decision = ApprovalStatus.Approved,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                OnApprovedFinal = outcome =>
                {
                    ApplyTransfer(asset, transfer);
                    _unitOfWork.Repository<AssetTransfer>().Update(transfer);
                    _unitOfWork.Repository<Asset>().Update(asset);
                },
                WriteNotifications = uow =>
                {
                    if (isFinalStage)
                    {
                        NotificationHelper.AddNotification(
                        uow,
                        _outboxWriter,
                        _organizationScope,
                            transfer.RequestedById,
                            NotificationType.General,
                            "Transfer approved",
                            "Transfer #" + transfer.Id + " for asset " + (asset.AssetTag ?? ("#" + asset.Id)) + " has been fully approved.",
                            "/Assets/Details/" + transfer.AssetId);
                    }
                    else
                    {
                        NotificationHelper.AddNotification(
                        uow,
                        _outboxWriter,
                        _organizationScope,
                            transfer.RequestedById,
                            NotificationType.PendingApproval,
                            "Transfer stage approved",
                            "Transfer #" + transfer.Id + " moved to Stage " + transfer.CurrentApprovalStage + " approval.",
                            "/Assets/Details/" + transfer.AssetId);
                        NotificationHelper.AddStageApproverNotification(
                            uow,
                            _outboxWriter,
                            _organizationScope,
                            _userService,
                            ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, transfer.CurrentApprovalStage, ApprovalProcessCodes.Transfer),
                            ApprovalWorkflowHelper.GetStageUserId(stageUserIds, transfer.CurrentApprovalStage),
                            "Transfer approval stage advanced",
                            "Transfer #" + transfer.Id + " is now awaiting Stage " + transfer.CurrentApprovalStage + " approval.",
                            "/Assets/Details/" + transfer.AssetId);
                    }
                }
            });

            _auditWriter.Write(
                "Assets.Transfer",
                nameof(AssetTransfer),
                transfer.Id.ToString(),
                isFinalStage ? "PendingApproval" : "Stage" + stageNumber,
                isFinalStage ? "Approved" : "Stage" + transfer.CurrentApprovalStage);

            if (isFinalStage)
            {
                _webhookService.QueueDelivery(
                    "transfer.approved",
                    "{\"transferId\":" + transfer.Id + ",\"assetId\":" + transfer.AssetId + "}");
            }
        }

        public void RejectTransfer(TransferApprovalDecisionVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin)
        {
            if (model == null)
            {
                throw new BusinessException("Transfer rejection payload is required.");
            }

            var transfer = GetPendingTransfer(model.TransferId);
            EnsureCanAccessTransferAsset(transfer);
            var asset = _unitOfWork.Repository<Asset>().GetById(transfer.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            var stageRoleIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(transfer.ApprovalStageRoleIds);
            var stageUserIds = ApprovalWorkflowSettingsHelper.ParseStageUserIds(transfer.ApprovalStageUserIds);
            var stageNumber = transfer.CurrentApprovalStage <= 0 ? 1 : transfer.CurrentApprovalStage;
            var expectedRoleId = ApprovalWorkflowHelper.GetStageRoleId(stageRoleIds, stageNumber, ApprovalProcessCodes.Transfer);
            var expectedUserId = ApprovalWorkflowHelper.GetStageUserId(stageUserIds, stageNumber);

            _approvalEngine.ExecuteStageDecision(new ApprovalStageDecisionRequest
            {
                ProcessCode = ApprovalProcessCodes.Transfer,
                ActingUserId = rejectedByUserId,
                ApproverRoleId = approverRoleId,
                IsSuperAdmin = isSuperAdmin,
                RequesterUserId = transfer.RequestedById,
                Notes = NormalizeText(model.Notes),
                Decision = ApprovalStatus.Rejected,
                RequestEntity = transfer,
                RequestEntityType = typeof(AssetTransfer),
                StageNumber = stageNumber,
                StageRoleIds = stageRoleIds,
                ExpectedRoleId = expectedRoleId,
                ExpectedUserId = expectedUserId,
                ApprovalActionEntity = new TransferApprovalAction
                {
                    AssetTransferId = transfer.Id,
                    StageNumber = stageNumber,
                    RoleId = expectedRoleId,
                    ApproverUserId = rejectedByUserId,
                    Decision = ApprovalStatus.Rejected,
                    Notes = NormalizeText(model.Notes),
                    DecisionDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                OnRejected = outcome =>
                {
                    asset.CurrentStatus = transfer.OriginalAssetStatus;
                    asset.UpdatedAt = DateTime.UtcNow;
                    _unitOfWork.Repository<Asset>().Update(asset);
                    _unitOfWork.Repository<AssetTransfer>().Update(transfer);
                },
                WriteNotifications = uow =>
                {
                    NotificationHelper.AddNotification(
                        uow,
                        _outboxWriter,
                        _organizationScope,
                        transfer.RequestedById,
                        NotificationType.General,
                        "Transfer rejected",
                        "Transfer #" + transfer.Id + " was rejected at Stage " + stageNumber + ".",
                        "/Assets/Details/" + transfer.AssetId);
                }
            });

            _auditWriter.Write("Assets.Transfer", nameof(AssetTransfer), transfer.Id.ToString(), "PendingApproval", "Rejected");
        }

        private AssetTransfer CreateTransferRecord(Asset asset, AssetTransferVm model, string requestedByUserId)
        {
            var fromUserId = NormalizeId(model.FromUserId) ?? NormalizeId(asset.CurrentCustodianId);
            var fromDepartmentId = model.FromDepartmentId ?? asset.DepartmentId;

            return new AssetTransfer
            {
                AssetId = model.AssetId,
                FromUserId = fromUserId,
                ToUserId = NormalizeId(model.ToUserId),
                FromDepartmentId = fromDepartmentId,
                ToDepartmentId = model.ToDepartmentId,
                Reason = NormalizeText(model.Reason),
                ConditionBefore = NormalizeText(model.ConditionBefore),
                ConditionAfter = NormalizeText(model.ConditionAfter),
                MissingAccessories = model.MissingAccessories,
                DamageNotes = NormalizeText(model.DamageNotes),
                RequestedById = requestedByUserId,
                TransferDate = DateTime.UtcNow,
                OriginalAssetStatus = asset.CurrentStatus,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        private void ApplyTransfer(Asset asset, AssetTransfer transfer)
        {
            ValidateTransferTargetsAtApproval(asset, transfer);

            _unitOfWork.Repository<AssetCustodyEvent>().Add(new AssetCustodyEvent
            {
                AssetId = transfer.AssetId,
                ActionType = CustodyActionType.Transferred,
                ActionDate = transfer.TransferDate,
                FromUserId = transfer.FromUserId,
                ToUserId = transfer.ToUserId,
                FromDepartmentId = transfer.FromDepartmentId,
                ToDepartmentId = transfer.ToDepartmentId,
                ConditionBefore = transfer.ConditionBefore,
                ConditionAfter = transfer.ConditionAfter,
                Reason = transfer.Reason,
                ApprovedById = transfer.ApprovedById,
                Notes = transfer.DamageNotes,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });

            if (!string.IsNullOrWhiteSpace(transfer.ToUserId))
            {
                asset.CurrentCustodianId = transfer.ToUserId;
            }
            else if (transfer.ToDepartmentId.HasValue)
            {
                asset.CurrentCustodianId = null;
            }

            if (transfer.ToDepartmentId.HasValue)
            {
                asset.DepartmentId = transfer.ToDepartmentId.Value;
            }

            asset.CurrentStatus = AssetStatus.Assigned;
            asset.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Asset>().Update(asset);
        }

        private void ValidateTransferTargetsAtApproval(Asset asset, AssetTransfer transfer)
        {
            var expectedFromUserId = NormalizeId(asset.CurrentCustodianId);
            if (!string.IsNullOrWhiteSpace(transfer.FromUserId)
                && !string.IsNullOrWhiteSpace(expectedFromUserId)
                && !string.Equals(transfer.FromUserId, expectedFromUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("Current custodian no longer matches the transfer request. Reject and resubmit.");
            }

            var expectedFromDepartmentId = asset.DepartmentId > 0 ? (int?)asset.DepartmentId : null;
            if (transfer.FromDepartmentId.HasValue
                && expectedFromDepartmentId.HasValue
                && transfer.FromDepartmentId.Value != expectedFromDepartmentId.Value)
            {
                throw new BusinessException("Current department no longer matches the transfer request. Reject and resubmit.");
            }

            EnsureUserBelongsToDepartment(transfer.ToUserId, transfer.ToDepartmentId);
        }

        private void EnsureTransferIsAllowed(Asset asset, AssetTransferVm model)
        {
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
        }

        private void EnsureTransferFieldIntegrity(Asset asset, AssetTransferVm model)
        {
            var expectedFromDepartmentId = asset.DepartmentId > 0 ? (int?)asset.DepartmentId : null;
            if (model.FromDepartmentId.HasValue && expectedFromDepartmentId.HasValue
                && model.FromDepartmentId.Value != expectedFromDepartmentId.Value)
            {
                throw new BusinessException("From department does not match the asset record.");
            }

            var expectedFromUserId = NormalizeId(asset.CurrentCustodianId);
            var submittedFromUserId = NormalizeId(model.FromUserId);
            if (!string.IsNullOrWhiteSpace(submittedFromUserId)
                && !string.IsNullOrWhiteSpace(expectedFromUserId)
                && !string.Equals(submittedFromUserId, expectedFromUserId, StringComparison.OrdinalIgnoreCase))
            {
                throw new BusinessException("From user does not match the asset custodian.");
            }

            EnsureUserBelongsToDepartment(model.ToUserId, model.ToDepartmentId);
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

        private AssetTransfer GetPendingTransfer(int transferId)
        {
            var transfer = _unitOfWork.Repository<AssetTransfer>().GetById(transferId);
            if (transfer == null)
            {
                throw new BusinessException("Transfer request not found.");
            }

            if (transfer.ApprovalStatus != ApprovalStatus.Pending)
            {
                throw new BusinessException("This transfer request is no longer pending approval.");
            }

            return transfer;
        }

        private void EnsureCanAccessTransferAsset(AssetTransfer transfer)
        {
            if (transfer == null)
            {
                throw new BusinessException("Transfer request not found.");
            }

            var asset = _unitOfWork.Repository<Asset>().GetById(transfer.AssetId);
            if (asset == null)
            {
                throw new BusinessException("Asset not found.");
            }

            _departmentScope.EnsureCanAccessAsset(asset);
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
