using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Web.ViewModels
{
    public class AssetCategoryVm
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category Name")]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Default useful life (months)")]
        public int? DefaultUsefulLifeMonths { get; set; }

        [Display(Name = "Default depreciation life (months)")]
        public int? DefaultDepreciationLifeMonths { get; set; }

        [Display(Name = "Default annual depreciation rate (%)")]
        public decimal? DefaultDepreciationRatePercent { get; set; }
    }

    public class AssetTypeVm
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int AssetCategoryId { get; set; }

        [Required]
        [Display(Name = "Asset Type Name")]
        public string Name { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Use custom useful life")]
        public bool UseCustomUsefulLife { get; set; }

        [Display(Name = "Useful life (months)")]
        public int? UsefulLifeMonths { get; set; }

        [Display(Name = "Override category depreciation life")]
        public bool UseCustomDepreciationLife { get; set; }

        [Display(Name = "Depreciation life (months)")]
        public int? DepreciationLifeMonths { get; set; }

        [Display(Name = "Override category depreciation rate")]
        public bool UseCustomDepreciationRate { get; set; }

        [Display(Name = "Annual depreciation rate (%)")]
        public decimal? DepreciationRatePercent { get; set; }
    }
}
