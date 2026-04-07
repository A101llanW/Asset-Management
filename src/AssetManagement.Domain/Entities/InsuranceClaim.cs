using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class InsuranceClaim : AuditableEntity
    {
        public int Id { get; set; }

        public string ClaimNumber { get; set; }

        public int AssetId { get; set; }

        public int? IncidentId { get; set; }

        public DateTime ClaimDate { get; set; }

        public string ClaimType { get; set; }

        public string Insurer { get; set; }

        public string Assessor { get; set; }

        public string DocumentsSubmitted { get; set; }

        public ClaimStatus ClaimStatus { get; set; }

        public decimal ApprovedAmount { get; set; }

        public DateTime? SettlementDate { get; set; }

        public string SettlementNotes { get; set; }

        public virtual Asset Asset { get; set; }

        public virtual AssetIncident Incident { get; set; }
    }
}
