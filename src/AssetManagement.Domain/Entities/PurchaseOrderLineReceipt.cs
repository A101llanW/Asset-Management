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
    }
}
