using AssetManagement.Domain.Common;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// Optional requisition approval override on a catalog node (category / type / sub-type), migration 100.
    /// Resolution per requisition line: sub-type, then type, then category, then the department inherit chain,
    /// then the organization Purchase matrix. FlowMode semantics mirror department units:
    /// InheritParent = no override at this node (continue the cascade), Custom = these stages (empty = auto-approve),
    /// AutoApprove = explicit zero stages. PO raiser fields follow the node that wins.
    /// </summary>
    public class CatalogRequisitionFlow : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public CatalogFlowScope Scope { get; set; }

        /// <summary>AssetCategory.Id, AssetType.Id or AssetSubType.Id depending on <see cref="Scope"/>.</summary>
        public int ScopedEntityId { get; set; }

        public RequisitionFlowMode FlowMode { get; set; }

        /// <summary>Comma-separated stage role ids (same format as Department.CustomStageRoleIds).</summary>
        public string CustomStageRoleIds { get; set; }

        /// <summary>Comma-separated stage user ids aligned with the role ids (blank = any holder of the role).</summary>
        public string CustomStageUserIds { get; set; }

        public int? PurchaseOrderRaiserRoleId { get; set; }

        public string PurchaseOrderRaiserUserId { get; set; }
    }
}
