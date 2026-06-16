using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public enum ImpersonationRequestStatus
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
        Expired,
        Active
    }

    public class ImpersonationRequest : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public virtual Organization Organization { get; set; }

        public string RequestedBy { get; set; }

        public string RequestedFrom { get; set; }

        public DateTime RequestDate { get; set; }

        public ImpersonationRequestStatus Status { get; set; }

        public string Reason { get; set; }

        public DateTime? DecisionDate { get; set; }

        public string AdminNotes { get; set; }

        public DateTime? ExpiryDate { get; set; }
    }
}
