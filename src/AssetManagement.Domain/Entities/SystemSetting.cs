using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class SystemSetting : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string SettingKey { get; set; }

        public string SettingValue { get; set; }

        public string Description { get; set; }
    }
}
