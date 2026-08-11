using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetDocumentRequirement : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetId { get; set; }

        public string ProcessType { get; set; }

        public int ProcessId { get; set; }

        public string DocumentType { get; set; }

        public string Label { get; set; }

        public int? DocumentId { get; set; }

        public DateTime? FulfilledAt { get; set; }

        public virtual Asset Asset { get; set; }

        public virtual AssetDocument Document { get; set; }
    }
}
