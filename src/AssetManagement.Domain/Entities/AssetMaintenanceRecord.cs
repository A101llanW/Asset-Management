using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class AssetMaintenanceRecord : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string MaintenanceTicketNumber { get; set; }

        public int AssetId { get; set; }

        public string ReportedIssue { get; set; }

        public MaintenanceType MaintenanceType { get; set; }

        public string ReportedById { get; set; }

        public string AssignedTechnicianOrVendor { get; set; }

        public DateTime ServiceDate { get; set; }

        public DateTime? CompletionDate { get; set; }

        public decimal Cost { get; set; }

        public string Downtime { get; set; }

        public string ReplacedParts { get; set; }

        public string Outcome { get; set; }

        public MaintenanceStatus Status { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
