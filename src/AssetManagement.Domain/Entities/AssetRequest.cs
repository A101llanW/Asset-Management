using System;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class AssetRequest : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string RequestedById { get; set; }

        public int? DepartmentId { get; set; }

        public int? CategoryId { get; set; }

        public int? RequestedAssetId { get; set; }

        public string RequestedAssetTag { get; set; }

        public string Justification { get; set; }

        public AssetRequestStatus Status { get; set; }

        public int? FulfilledAssetId { get; set; }

        public string ReviewedById { get; set; }

        public DateTime? ReviewedAt { get; set; }

        public string ReviewNotes { get; set; }

        public byte[] RowVersion { get; set; }

        public virtual Department Department { get; set; }

        public virtual AssetCategory Category { get; set; }

        public virtual Asset RequestedAsset { get; set; }

        public virtual Asset FulfilledAsset { get; set; }
    }
}
