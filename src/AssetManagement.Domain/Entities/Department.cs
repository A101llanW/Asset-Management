using System.Collections.Generic;
using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    public class Department : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string Name { get; set; }

        public string Code { get; set; }

        public string Description { get; set; }

        public int? ParentDepartmentId { get; set; }

        public DepartmentKind DepartmentKind { get; set; }

        public bool IsRequisitionTarget { get; set; }

        public virtual ICollection<Asset> Assets { get; set; } = new HashSet<Asset>();
    }
}

