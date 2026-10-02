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

        // PR14 / room-approval columns (IIS migration 065)
        public bool UseCustomRequisitionApproval { get; set; }

        public string RequisitionApprovalStageRoleIds { get; set; }

        public string RequisitionApprovalStageUserIds { get; set; }

        /// <summary>Optional Class PIC (Kind=Class). Never required; no cascade reassign.</summary>
        public string ClassTeacherUserId { get; set; }

        /// <summary>Optional Room PIC (Kind=Room). Never required; no cascade reassign.</summary>
        public string RoomCustodianUserId { get; set; }

        public virtual ICollection<Asset> Assets { get; set; } = new HashSet<Asset>();
    }
}
