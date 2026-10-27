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

        /// <summary>
        /// Migration 108: the "Item / asset name" for the line; becomes the received asset's name. NULL on rows
        /// created before 108 (receiving then uses the legacy description / sub-type / brand-model fallback).
        /// </summary>
        public string ItemName { get; set; }

        /// <summary>
        /// Migration 108: the existing asset this line is tagged to (per line, so a split batch keeps each line's tag).
        /// The tag only supplies the classification and pre-fills the name: receiving always creates a NEW asset and
        /// never updates the tagged one. No navigation property on purpose (the reflection map would bind it).
        /// </summary>
        public int? TargetAssetId { get; set; }

        public virtual PurchaseRequest PurchaseRequest { get; set; }
    }
}
