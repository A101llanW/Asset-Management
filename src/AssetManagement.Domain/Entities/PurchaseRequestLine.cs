using System;
using AssetManagement.Domain.Common;

namespace AssetManagement.Domain.Entities
{
    public class PurchaseRequestLine : AuditableEntity, ITenantEntity
    {
        public int Id { get; set; }

        public int? OrganizationId { get; set; }

        public int PurchaseRequestId { get; set; }

        public int LineNumber { get; set; }

        public string Description { get; set; }

        public int Quantity { get; set; }

        /// <summary>Optional catalog classification (migration 100); normalized so sub-type implies type and category.</summary>
        public int? AssetCategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public virtual PurchaseRequest PurchaseRequest { get; set; }
    }
}
