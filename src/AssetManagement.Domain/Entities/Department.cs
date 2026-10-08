using System.Collections.Generic;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class Department : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string Name { get; set; }

        public string Code { get; set; }

        public string Description { get; set; }

        public int? ParentDepartmentId { get; set; }

        public DepartmentKind DepartmentKind { get; set; }

        public bool IsRequisitionTarget { get; set; }

        public RequisitionFlowMode RequisitionFlowMode { get; set; }

        public string CustomStageRoleIds { get; set; }

        public string CustomStageUserIds { get; set; }

        /// <summary>
        /// Inherit-mode only (migration 085). Null = nearest ancestor (legacy behaviour);
        /// SubDepartment / Department = inherit that specific ancestor's flow.
        /// </summary>
        public RequisitionInheritSource? RequisitionInheritFrom { get; set; }

        // PR14 / room-approval columns (IIS migration 065)
        public bool UseCustomRequisitionApproval { get; set; }

        public string RequisitionApprovalStageRoleIds { get; set; }

        public string RequisitionApprovalStageUserIds { get; set; }

        /// <summary>
        /// Migration 088. Custom-flow units only: role that raises the purchase order after final approval.
        /// Null = not configured (legacy: anyone with Purchases permissions). Resolved from the unit the
        /// requisition flow is effectively taken from (same resolver path as the approval stages).
        /// </summary>
        public int? PurchaseOrderRaiserRoleId { get; set; }

        /// <summary>Migration 088. Optional person within <see cref="PurchaseOrderRaiserRoleId"/>; null = any active role holder.</summary>
        public string PurchaseOrderRaiserUserId { get; set; }

        /// <summary>Optional Class PIC (Kind=Class). Never required; no cascade reassign.</summary>
        public string ClassTeacherUserId { get; set; }

        /// <summary>Optional Room PIC (Kind=Room). Never required; no cascade reassign.</summary>
        public string RoomCustodianUserId { get; set; }

        public virtual ICollection<Asset> Assets { get; set; } = new HashSet<Asset>();
    }
}