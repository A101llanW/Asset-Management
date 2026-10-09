using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// rel1009i (migration 098): one line of a purchase order (PurchaseRecord), prefilled from a requisition line.
    /// PurchaseRequestLineId is NULL when the requisition has no line items (single-item requisition).
    /// The classification (category / type / sub-type) is what Receiving auto-fills read-only.
    /// </summary>
    public class PurchaseOrderLine : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int PurchaseRecordId { get; set; }

        public int? PurchaseRequestLineId { get; set; }

        public int LineNumber { get; set; }

        public string Description { get; set; }

        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public int? AssetCategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }

        /// <summary>Where the classification came from: RequisitionAsset, SelectedOnOrder, SupplierCatalog (NULL = none).</summary>
        public string ClassificationSource { get; set; }
    }
}
