using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class DisposalRecord : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetId { get; set; }

        public DateTime DisposalRequestDate { get; set; }

        public string DisposalApprovedById { get; set; }

        public string DisposalReason { get; set; }

        public DisposalMethod DisposalMethod { get; set; }

        public DateTime? DisposalDate { get; set; }

        public decimal? DisposalAmount { get; set; }

        public ApprovalStatus ApprovalStatus { get; set; }

        public string Notes { get; set; }

        public string RequestedById { get; set; }

        public int CurrentApprovalStage { get; set; }

        public string ApprovalStageRoleIds { get; set; }

        public string ApprovalStageUserIds { get; set; }

        public AssetStatus OriginalAssetStatus { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
