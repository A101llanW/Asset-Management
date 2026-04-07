using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class AssetType : AuditableEntity
    {
        public int Id { get; set; }

        public int AssetCategoryId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public virtual AssetCategory AssetCategory { get; set; }
    }
}
