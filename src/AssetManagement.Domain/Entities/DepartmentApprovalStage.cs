using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// Normalized approval stages for a department Custom flow.
    /// Dual-read with Department.CustomStageRoleIds CSV during migration window.
    /// </summary>
    public class DepartmentApprovalStage : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int DepartmentId { get; set; }

        public int StageNumber { get; set; }

        public int RoleId { get; set; }

        public string UserId { get; set; }

        public byte[] RowVersion { get; set; }
    }
}