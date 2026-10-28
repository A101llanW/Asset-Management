using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// rel1009i (migration 098): links each received unit (AssetReceiving, one per asset) to its purchase order line,
    /// so received / outstanding is tracked per line and over-receipt is blocked per line (cumulative).
    /// </summary>
    public class PurchaseOrderLineReceipt : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int PurchaseRecordId { get; set; }

        public int PurchaseOrderLineId { get; set; }

        public int AssetReceivingId { get; set; }

        public int AssetId { get; set; }

        public int QuantityReceived { get; set; }
        /// <summary>Migration 108 (C3): who changed the classification at receiving (the received asset got the new one; "original" = the PO line's) (user id); NULL = never changed.</summary>
        public string ClassificationOverriddenById { get; set; }

        /// <summary>Migration 108 (C3): when the classification was changed (UTC).</summary>
        public System.DateTime? ClassificationOverriddenAt { get; set; }

        /// <summary>Migration 108 (C3): the classification before the change (snapshot ids, no FK).</summary>
        public int? OriginalAssetCategoryId { get; set; }

        public int? OriginalAssetTypeId { get; set; }

        public int? OriginalAssetSubTypeId { get; set; }

        /// <summary>Migration 108 (C3): the classification the change set, as recorded at receiving (snapshot ids, no FK),
        /// so the trail does not follow later reclassification of the asset. NULL on rows stamped before these columns.</summary>
        public int? NewAssetCategoryId { get; set; }

        public int? NewAssetTypeId { get; set; }

        public int? NewAssetSubTypeId { get; set; }
    }
}
