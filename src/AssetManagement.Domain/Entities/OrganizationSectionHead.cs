using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// Migration 112: the company-wide head of one school section (KG / Prep / Senior school).
    /// One row per organisation per section. Null HeadUserId = not set (approvals then use department
    /// section heads from 107/111, else the Department Head role holders).
    /// </summary>
    public class OrganizationSectionHead : ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public DepartmentSection Section { get; set; }

        public string HeadUserId { get; set; }
    }
}
