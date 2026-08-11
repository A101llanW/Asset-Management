using AssetManagement.Domain.Common;
namespace AssetManagement.Domain.Entities
{
    public class AssetSubType : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }
        public int? OrganizationId { get; set; }
        public int AssetTypeId { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Specifications { get; set; }
        public string Sku { get; set; }
        public virtual AssetType AssetType { get; set; }
    }
}
