using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class AssetCustodyEvent : AuditableEntity
    {
        public int Id { get; set; }

        public int AssetId { get; set; }

        public CustodyActionType ActionType { get; set; }

        public DateTime ActionDate { get; set; }

        public string FromUserId { get; set; }

        public string ToUserId { get; set; }

        public int? FromDepartmentId { get; set; }

        public int? ToDepartmentId { get; set; }

        public string ConditionBefore { get; set; }

        public string ConditionAfter { get; set; }

        public string Reason { get; set; }

        public string ApprovedById { get; set; }

        public string Notes { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
