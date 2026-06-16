using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetType : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int AssetCategoryId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int? UsefulLifeMonths { get; set; }

        public virtual AssetCategory AssetCategory { get; set; }
    }
}
