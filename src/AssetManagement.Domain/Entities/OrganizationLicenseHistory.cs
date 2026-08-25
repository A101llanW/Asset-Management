using System;

namespace AssetManagement.Domain.Entities
{
    public class OrganizationLicenseHistory
    {
        public int Id { get; set; }

        public int OrganizationLicenseId { get; set; }

        public int OrganizationId { get; set; }

        public string Action { get; set; }

        public DateTime? PreviousExpiryDate { get; set; }

        public DateTime? NewExpiryDate { get; set; }

        public string PreviousStatus { get; set; }

        public string NewStatus { get; set; }

        public string PerformedBy { get; set; }

        public string Reason { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
