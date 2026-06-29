using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
namespace AssetManagement.Application.ViewModels
{
    public class SupplierCatalogItemVm
    {
        public int Id { get; set; }

        public int SupplierId { get; set; }

        [Required]
        [StringLength(200)]
        public string ItemName { get; set; }

        [StringLength(2000)]
        public string ItemDescription { get; set; }

        [StringLength(100)]
        public string Sku { get; set; }

        public int? AssetCategoryId { get; set; }

        public string AssetCategoryName { get; set; }

        public int? AssetTypeId { get; set; }

        public string AssetTypeName { get; set; }

        /// <summary>Optional link to an existing asset record (tag) for receive/assignment.</summary>
        public int? TaggedAssetId { get; set; }

        public string TaggedAssetTag { get; set; }

        public string TaggedAssetName { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }

        [StringLength(10)]
        public string Currency { get; set; }

        public int? MinimumOrderQuantity { get; set; }

        public int? LeadTimeDays { get; set; }

        public DateTime? EffectiveFrom { get; set; }

        public DateTime? EffectiveTo { get; set; }

        public bool IsActive { get; set; }
    }

    /// <summary>Supplier registration: profile plus initial catalog lines for PO price comparison.</summary>
    public class SupplierCreateVm : SupplierVm
    {
        public List<SupplierCatalogItemVm> CatalogItems { get; set; } = new List<SupplierCatalogItemVm>();
    }

    public class SupplierPriceComparisonRowVm
    {
        public int? CatalogItemId { get; set; }

        public int SupplierId { get; set; }

        public string SupplierName { get; set; }

        public string ItemLabel { get; set; }

        public decimal UnitPrice { get; set; }

        public string Currency { get; set; }

        public int? LeadTimeDays { get; set; }

        public bool IsPreferred { get; set; }

        public bool IsHistorical { get; set; }

        public bool IsCheapest { get; set; }

        public bool IsMostExpensive { get; set; }
    }

    public class SupplierPriceComparisonResultVm
    {
        public string ItemDescription { get; set; }

        public decimal? RequisitionEstimatedUnitCost { get; set; }

        public string Currency { get; set; }

        public IList<SupplierPriceComparisonRowVm> Rows { get; set; } = new List<SupplierPriceComparisonRowVm>();

        public bool HasCatalogMatches { get; set; }

        public bool HasHistoricalFallback { get; set; }
    }
}
