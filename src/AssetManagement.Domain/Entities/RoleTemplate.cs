using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class RoleTemplate : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        /// <summary>Comma-separated permission ids captured from a role.</summary>
        public string PermissionIds { get; set; }

        public int? SourceRoleId { get; set; }
    }
}
