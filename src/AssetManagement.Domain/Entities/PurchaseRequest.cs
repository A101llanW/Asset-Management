using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class PurchaseRequest : AuditableEntity
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string RequestedById { get; set; }

        public string ApprovedById { get; set; }

        public ApprovalStatus ApprovalStatus { get; set; }

        public int DepartmentId { get; set; }

        public string Justification { get; set; }

        public decimal EstimatedUnitCost { get; set; }

        public int Quantity { get; set; }

        public string Currency { get; set; }

        public string Notes { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public virtual Department Department { get; set; }
    }
}
