using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class AssetTransfer : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetId { get; set; }

        public string FromUserId { get; set; }

        public string ToUserId { get; set; }

        public int? FromDepartmentId { get; set; }

        public int? ToDepartmentId { get; set; }

        public string Reason { get; set; }

        public string ConditionBefore { get; set; }

        public string ConditionAfter { get; set; }

        public bool MissingAccessories { get; set; }

        public string DamageNotes { get; set; }

        public ApprovalStatus ApprovalStatus { get; set; }

        public string ApprovedById { get; set; }

        public string RequestedById { get; set; }

        public int CurrentApprovalStage { get; set; }

        public string ApprovalStageRoleIds { get; set; }

        public string ApprovalStageUserIds { get; set; }

        public DateTime TransferDate { get; set; }

        public AssetStatus OriginalAssetStatus { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Asset Asset { get; set; }

        public virtual Department FromDepartment { get; set; }

        public virtual Department ToDepartment { get; set; }
    }
}
