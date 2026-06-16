using System;
using System.Data.SqlClient;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.DTOs;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Services
{
    public class ApprovalWorkflowEngine : IApprovalWorkflowEngine
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;

        public ApprovalWorkflowEngine(IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
        }

        public void ExecuteStageDecision(ApprovalStageDecisionRequest request)
        {
            if (request == null)
            {
                throw new BusinessException("Approval decision request is required.");
            }

            if (request.RequestEntity == null || request.RequestEntityType == null)
            {
                throw new BusinessException("Approval request entity is required.");
            }

            if (request.ApprovalActionEntity == null)
            {
                throw new BusinessException("Approval action entity is required.");
            }

            ApprovalWorkflowHelper.EnsureUserCanApprove(
                _unitOfWork,
                _auditWriter,
                request.ApproverRoleId,
                request.IsSuperAdmin,
                request.ExpectedRoleId,
                request.ExpectedUserId,
                request.RequesterUserId,
                request.ActingUserId,
                request.ProcessCode);

            var isApprove = request.Decision == ApprovalStatus.Approved;
            var isReject = request.Decision == ApprovalStatus.Rejected;
            if (!isApprove && !isReject)
            {
                throw new BusinessException("Only approve or reject decisions are supported.");
            }

            var isFinalStage = isApprove && request.StageNumber >= request.StageRoleIds.Count;
            var outcome = new ApprovalStageOutcome
            {
                IsFinalStage = isFinalStage,
                NewStage = isApprove && !isFinalStage ? request.StageNumber + 1 : request.StageNumber
            };

            try
            {
                _unitOfWork.ExecuteInTransaction(() =>
                {
                    _unitOfWork.TrackAdd(request.ApprovalActionEntity);
                    ApplyRequestMutation(request, isApprove, isFinalStage, outcome);
                    _unitOfWork.PersistConditionalApprovalUpdate(request.RequestEntity, request.StageNumber);

                    if (isReject)
                    {
                        if (request.OnRejected != null)
                        {
                            request.OnRejected(outcome);
                        }
                    }
                    else if (isFinalStage)
                    {
                        if (request.OnApprovedFinal != null)
                        {
                            request.OnApprovedFinal(outcome);
                        }
                    }
                    else if (request.OnApprovedIntermediate != null)
                    {
                        request.OnApprovedIntermediate(outcome);
                    }

                    if (request.WriteNotifications != null)
                    {
                        request.WriteNotifications(_unitOfWork);
                    }
                });
            }
            catch (SqlException ex)
            {
                if (ex.Number == 2601 || ex.Number == 2627)
                {
                    throw new BusinessException("This approval stage has already been recorded by another approver.");
                }

                throw;
            }
        }

        private static void ApplyRequestMutation(
            ApprovalStageDecisionRequest request,
            bool isApprove,
            bool isFinalStage,
            ApprovalStageOutcome outcome)
        {
            var approvalStatusProperty = request.RequestEntityType.GetProperty("ApprovalStatus");
            var stageProperty = request.RequestEntityType.GetProperty("CurrentApprovalStage");
            var updatedAtProperty = request.RequestEntityType.GetProperty("UpdatedAt");
            var approvedByProperty = request.RequestEntityType.GetProperty("ApprovedById");
            var approvedAtProperty = request.RequestEntityType.GetProperty("ApprovedAt");

            if (isApprove && isFinalStage)
            {
                approvalStatusProperty.SetValue(request.RequestEntity, ApprovalStatus.Approved, null);
                stageProperty.SetValue(request.RequestEntity, 0, null);
                if (approvedByProperty != null)
                {
                    approvedByProperty.SetValue(request.RequestEntity, request.ActingUserId, null);
                }

                if (approvedAtProperty != null)
                {
                    approvedAtProperty.SetValue(request.RequestEntity, DateTime.UtcNow, null);
                }
            }
            else if (isApprove)
            {
                stageProperty.SetValue(request.RequestEntity, outcome.NewStage, null);
            }
            else
            {
                approvalStatusProperty.SetValue(request.RequestEntity, ApprovalStatus.Rejected, null);
                if (approvedByProperty != null)
                {
                    approvedByProperty.SetValue(request.RequestEntity, request.ActingUserId, null);
                }
            }

            if (updatedAtProperty != null)
            {
                updatedAtProperty.SetValue(request.RequestEntity, DateTime.UtcNow, null);
            }
        }
    }
}
