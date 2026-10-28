using AssetManagement.Domain.Common;
namespace AssetManagement.Domain.Entities
{
    public class AssetSubType : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public int AssetTypeId { get; set; }
        public string Name { get; set; }

        /// <summary>Migration 110: AspNetUsers.Id of who created it inline (requisition form / receiving); NULL otherwise.</summary>
        public string CreatedById { get; set; }

        /// <summary>Migration 110: where it was created inline: "Requisition" or "Receiving" (ClassificationCreateOrigin); NULL otherwise.</summary>
        public string CreatedVia { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Specifications { get; set; }
        public string Sku { get; set; }

        public decimal? DefaultAcquisitionCost { get; set; }

        public virtual AssetType AssetType { get; set; }
    }
}
