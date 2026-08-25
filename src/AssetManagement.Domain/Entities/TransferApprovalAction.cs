using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class TransferApprovalAction : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetTransferId { get; set; }

        public int StageNumber { get; set; }

        public int RoleId { get; set; }

        public string ApproverUserId { get; set; }

        public ApprovalStatus Decision { get; set; }

        public string Notes { get; set; }

        public DateTime DecisionDate { get; set; }

        public virtual AssetTransfer AssetTransfer { get; set; }
    }
}
