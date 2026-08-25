using System.Collections.Generic;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class Permission : AuditableEntity
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Code { get; set; }

        public string Module { get; set; }

        public string Description { get; set; }

        public virtual ICollection<RolePermission> RolePermissions { get; set; } = new HashSet<RolePermission>();
    }
}
