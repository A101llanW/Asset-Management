using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// Links the requisitions created from one submitted form when its lines resolve to different approval paths
    /// (MixedFlowPolicy = AutoSplit), migration 100. Each linked PurchaseRequest keeps its own number and approval
    /// snapshot; they share department, justification, notes and attachment.
    /// </summary>
    public class PurchaseRequestGroup : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        /// <summary>BATCH-000042 (assigned from the identity after insert).</summary>
        public string BatchLabel { get; set; }

        public string CreatedByUserId { get; set; }
    }
}
