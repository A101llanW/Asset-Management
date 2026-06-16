using System;
using System.Collections.Generic;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class PurchaseRequest : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string RequestNumber { get; set; }

        public string RequestedById { get; set; }

        public string ApprovedById { get; set; }

        public ApprovalStatus ApprovalStatus { get; set; }

        /// <summary>1-based stage while pending; 0 when not using staged approval or after legacy single-step.</summary>
        public int CurrentApprovalStage { get; set; }

        /// <summary>Comma-separated role ids snapshot at submission (same format as transfer/disposal).</summary>
        public string ApprovalStageRoleIds { get; set; }

        public int DepartmentId { get; set; }

        public string Justification { get; set; }

        public decimal EstimatedUnitCost { get; set; }

        public int Quantity { get; set; }

        public string Currency { get; set; }

        public string Notes { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Department Department { get; set; }

        public virtual ICollection<PurchaseApprovalAction> ApprovalActions { get; set; } = new HashSet<PurchaseApprovalAction>();
    }
}

