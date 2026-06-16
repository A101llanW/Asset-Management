using System.Collections.Generic;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class Role : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsSystemRole { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new HashSet<RolePermission>();
    }
}
