using System.Collections.Generic;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetCategory : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int? DefaultUsefulLifeMonths { get; set; }

        public virtual ICollection<AssetType> AssetTypes { get; set; } = new HashSet<AssetType>();
    }
}
