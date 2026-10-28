using System.Collections.Generic;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetCategory : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string Name { get; set; }

        /// <summary>Migration 110: AspNetUsers.Id of who created it inline (requisition form / receiving); NULL otherwise.</summary>
        public string CreatedById { get; set; }

        /// <summary>Migration 110: where it was created inline: "Requisition" or "Receiving" (ClassificationCreateOrigin); NULL otherwise.</summary>
        public string CreatedVia { get; set; }

        public string Description { get; set; }

        public int? DefaultUsefulLifeMonths { get; set; }

        public int? DefaultDepreciationLifeMonths { get; set; }

        public decimal? DefaultDepreciationRatePercent { get; set; }

        public virtual ICollection<AssetType> AssetTypes { get; set; } = new HashSet<AssetType>();
    }
}
