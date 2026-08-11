namespace AssetManagement.Application.ViewModels
{
    public class AssetCategoryListVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }

        public int? DefaultUsefulLifeMonths { get; set; }

        public int? DefaultDepreciationLifeMonths { get; set; }

        public decimal? DefaultDepreciationRatePercent { get; set; }

        public int TypeCount { get; set; }
    }

    public class AssetTypeListVm
    {
        public int Id { get; set; }

        public int AssetCategoryId { get; set; }

        public string CategoryName { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsActive { get; set; }

        public bool UseCustomUsefulLife { get; set; }

        public int? UsefulLifeMonths { get; set; }

        public bool UseCustomDepreciationLife { get; set; }

        public int? DepreciationLifeMonths { get; set; }

        public bool UseCustomDepreciationRate { get; set; }

        public decimal? DepreciationRatePercent { get; set; }
    }
}
