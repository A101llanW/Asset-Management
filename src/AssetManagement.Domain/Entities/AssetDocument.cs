using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetDocument : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetId { get; set; }

        public string DocumentType { get; set; }

        public string FileName { get; set; }

        public string FilePath { get; set; }

        public string ContentType { get; set; }

        public long FileSizeBytes { get; set; }

        public string UploadedById { get; set; }

        public DateTime UploadedAt { get; set; }

        public virtual Asset Asset { get; set; }
    }
}
