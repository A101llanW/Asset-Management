using System;
using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts
{
    public sealed class ApprovalStageDecisionRequest
    {
        public string ProcessCode { get; set; }

        public string ActingUserId { get; set; }

        public int? ApproverRoleId { get; set; }

        public bool IsSuperAdmin { get; set; }

        public string RequesterUserId { get; set; }

        public string Notes { get; set; }

        public ApprovalStatus Decision { get; set; }

        public object RequestEntity { get; set; }

        public Type RequestEntityType { get; set; }

        public int StageNumber { get; set; }

        public IList<int> StageRoleIds { get; set; }

        public int ExpectedRoleId { get; set; }

        public string ExpectedUserId { get; set; }

        public object ApprovalActionEntity { get; set; }

        public Type ApprovalActionEntityType { get; set; }

        public Action<ApprovalStageOutcome> OnApprovedIntermediate { get; set; }

        public Action<ApprovalStageOutcome> OnApprovedFinal { get; set; }

        public Action<ApprovalStageOutcome> OnRejected { get; set; }

        public Action<IUnitOfWork> WriteNotifications { get; set; }
    }

    public sealed class ApprovalStageOutcome
    {
        public bool IsFinalStage { get; set; }

        public int NewStage { get; set; }
    }

    public interface IApprovalWorkflowEngine
    {
        void ExecuteStageDecision(ApprovalStageDecisionRequest request);
    }
}
