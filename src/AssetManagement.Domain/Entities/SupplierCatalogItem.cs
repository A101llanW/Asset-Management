using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class SupplierCatalogItem : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int SupplierId { get; set; }

        public string ItemName { get; set; }

        public string ItemDescription { get; set; }

        public string Sku { get; set; }

        public int? AssetCategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? TaggedAssetId { get; set; }

        public decimal UnitPrice { get; set; }

        public string Currency { get; set; }

        public int? MinimumOrderQuantity { get; set; }

        public int? LeadTimeDays { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public virtual Supplier Supplier { get; set; }

        public virtual AssetCategory AssetCategory { get; set; }

        public virtual AssetType AssetType { get; set; }

        public virtual Asset TaggedAsset { get; set; }
    }
}
