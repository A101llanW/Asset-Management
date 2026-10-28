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

        /// <summary>Comma-separated user ids snapshot at submission (same format as transfer/disposal).</summary>
        public string ApprovalStageUserIds { get; set; }

        public int DepartmentId { get; set; }

        public string ItemDescription { get; set; }

        public string Justification { get; set; }

        public int? QuantityInStock { get; set; }

        public DateTime? RequiredDate { get; set; }

        public string OrderByUserId { get; set; }

        public int Quantity { get; set; }

        public string Currency { get; set; }

        public string Notes { get; set; }

        public string AttachmentFileName { get; set; }

        public string AttachmentFilePath { get; set; }

        public string AttachmentContentType { get; set; }

        public long? AttachmentFileSizeBytes { get; set; }

        /// <summary>Optional link to an existing asset record for post-purchase assignment.</summary>
        public int? TargetAssetId { get; set; }

        /// <summary>Set when this requisition was auto-split from one form with sibling requisitions (migration 100).</summary>
        public int? PurchaseRequestGroupId { get; set; }

        /// <summary>Winning approval source at submit, e.g. "Custom on Type: Laptops" (migration 100; null for older rows).</summary>
        public string ApprovalFlowSource { get; set; }

        /// <summary>
        /// Migration 110: "Room" or "Person" (RequisitionTargetRules); NULL on requisitions saved before 110, which
        /// keep their department as the target. DepartmentId stays the approval / scope department in every mode.
        /// </summary>
        public string RequisitionTargetKind { get; set; }

        /// <summary>Migration 110: the person a Person-target requisition is for (AspNetUsers.Id); NULL otherwise.</summary>
        public string TargetUserId { get; set; }

        /// <summary>Migration 110: Person target only. "PersonRoom" (asset recorded in the person's room) or "CompanyCustody" (no department, the person is custodian).</summary>
        public string TargetPlacement { get; set; }

        /// <summary>Migration 110: the person's room / class when TargetPlacement = PersonRoom; NULL otherwise.</summary>
        public int? TargetDepartmentId { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Department Department { get; set; }

        public virtual ICollection<PurchaseApprovalAction> ApprovalActions { get; set; } = new HashSet<PurchaseApprovalAction>();

        public virtual ICollection<PurchaseRequestLine> Lines { get; set; } = new HashSet<PurchaseRequestLine>();
    }
}
