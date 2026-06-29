using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class OrganizationLicense : AuditableEntity
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        public virtual Organization Organization { get; set; }

        public string Status { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime ExpiryDate { get; set; }

        public int? MaxUsers { get; set; }

        public DateTime? PausedAt { get; set; }

        public string PausedBy { get; set; }

        public string PauseReason { get; set; }

        public string Notes { get; set; }
    }
}
