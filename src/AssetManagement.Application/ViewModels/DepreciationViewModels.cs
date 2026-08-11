namespace AssetManagement.Application.ViewModels
{
    public class AssetDepreciationSettingsVm
    {
        public int? DepreciationLifeMonths { get; set; }

        public decimal? DepreciationRatePercent { get; set; }

        public bool UseCustomDepreciationLife { get; set; }

        public bool UseCustomDepreciationRate { get; set; }

        public int EffectiveLifeMonths { get; set; }

        public decimal EffectiveRatePercent { get; set; }

        public string LifeSource { get; set; }

        public string RateSource { get; set; }
    }
}
