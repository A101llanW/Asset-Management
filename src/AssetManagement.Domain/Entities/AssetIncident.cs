using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class AssetIncident : AuditableEntity
    {
        public int Id { get; set; }

        public string IncidentNumber { get; set; }

        public int AssetId { get; set; }

        public string ReportedById { get; set; }

        public IncidentType IncidentType { get; set; }

        public DateTime IncidentDate { get; set; }

        public string Description { get; set; }

        public string WitnessComments { get; set; }

        public string PoliceCaseReference { get; set; }

        public IncidentSeverity Severity { get; set; }

        public string LiabilityNotes { get; set; }

        public string ResolutionStatus { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
