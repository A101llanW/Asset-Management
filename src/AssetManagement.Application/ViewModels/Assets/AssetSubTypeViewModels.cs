using System.ComponentModel.DataAnnotations;
namespace AssetManagement.Application.ViewModels
{
    public class AssetSubTypeVm
    {
        public int Id { get; set; }
        public int AssetTypeId { get; set; }
        public string AssetTypeName { get; set; }
        public int AssetCategoryId { get; set; }
        public string AssetCategoryName { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Specifications { get; set; }
        public string Sku { get; set; }

        public decimal? DefaultAcquisitionCost { get; set; }

        public bool IsActive { get; set; }

        public int StockCount { get; set; }
    }
    public class AssetSubTypeListItemVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }

        public decimal? DefaultAcquisitionCost { get; set; }

        public bool IsActive { get; set; }

        public int StockCount { get; set; }
    }
    public class AssetSubTypeEditVm
    {
        public int Id { get; set; }
        [Required]
        public int AssetTypeId { get; set; }
        [Required]
        [StringLength(200)]
        public string Name { get; set; }
        [StringLength(100)]
        public string Brand { get; set; }
        [StringLength(100)]
        [Display(Name = "Model")]
        public string ItemModel { get; set; }
        public string Specifications { get; set; }
        [StringLength(100)]
        public string Sku { get; set; }

        [Range(0.01, 999999999)]
        [Display(Name = "Default acquisition cost")]
        public decimal? DefaultAcquisitionCost { get; set; }

        public decimal? PreviousDefaultAcquisitionCost { get; set; }

        /// <summary>individual or subtype — see AcquisitionCostApplyScopes.</summary>
        public string AcquisitionCostApplyScope { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// Sub-type details: the sub-type's active assets listed one by one (server-paged), each with its own
    /// price. A blank asset price shows "Not set" and is never filled from <see cref="AssetSubTypeVm.DefaultAcquisitionCost"/>.
    /// </summary>
    public class AssetSubTypeDetailsVm
    {
        public AssetSubTypeVm SubType { get; set; }

        public string AssetTypeName { get; set; }

        public string AssetCategoryName { get; set; }

        public AssetListPageVm Assets { get; set; } = new AssetListPageVm();

        /// <summary>Whole sub-type (ignores the search box): priced total and priced/unpriced counts.</summary>
        public AssetPriceSummaryVm PriceSummary { get; set; } = new AssetPriceSummaryVm();

        public string Search { get; set; }

        public int TotalPages { get; set; }

        public bool CanEditPrice { get; set; }
    }

    public class AssetSubTypeIndexItemVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Brand { get; set; }
        public string Model { get; set; }
        public string Sku { get; set; }
        public int AssetTypeId { get; set; }
        public string AssetTypeName { get; set; }
        public string AssetCategoryName { get; set; }
        public bool IsActive { get; set; }
        public decimal? DefaultAcquisitionCost { get; set; }
    }
    public class AssetSubTypeCreateFromAssetVm
    {
        [Required]
        public int AssetTypeId { get; set; }
        [Required]
        [StringLength(200)]
        public string Name { get; set; }
        [StringLength(100)]
        public string Brand { get; set; }
        [StringLength(100)]
        public string Model { get; set; }
        public string Specifications { get; set; }
        [StringLength(100)]
        public string Sku { get; set; }
    }
}
