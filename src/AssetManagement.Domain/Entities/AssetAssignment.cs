using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class AssetAssignment : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetId { get; set; }

        public int? ToDepartmentId { get; set; }

        public string ToUserId { get; set; }

        public AssignmentType AssignmentType { get; set; }

        public DateTime AssignedDate { get; set; }

        public DateTime? ExpectedReturnDate { get; set; }

        public string ConditionBeforeHandover { get; set; }

        public string AccessoriesHandedOver { get; set; }

        public string HandoverNotes { get; set; }

        public string HandedOverById { get; set; }

        public string ReceivedById { get; set; }

        public bool RecipientAcknowledged { get; set; }

        public DateTime? AcknowledgedAt { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Asset Asset { get; set; }

        public virtual Department ToDepartment { get; set; }
    }
}
