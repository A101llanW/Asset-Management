using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class RolePermission : ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int RoleId { get; set; }

        public int PermissionId { get; set; }

        public virtual Role Role { get; set; }

        public virtual Permission Permission { get; set; }
    }
}
