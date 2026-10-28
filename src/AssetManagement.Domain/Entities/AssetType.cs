using System.Collections.Generic;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetType : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetCategoryId { get; set; }

        public string Name { get; set; }

        /// <summary>Migration 110: AspNetUsers.Id of who created it inline (requisition form / receiving); NULL otherwise.</summary>
        public string CreatedById { get; set; }

        /// <summary>Migration 110: where it was created inline: "Requisition" or "Receiving" (ClassificationCreateOrigin); NULL otherwise.</summary>
        public string CreatedVia { get; set; }

        public string Description { get; set; }

        public int? UsefulLifeMonths { get; set; }

        public int? DepreciationLifeMonths { get; set; }

        public decimal? DepreciationRatePercent { get; set; }

        public virtual AssetCategory AssetCategory { get; set; }

        public virtual ICollection<AssetSubType> SubTypes { get; set; } = new HashSet<AssetSubType>();
    }
}
