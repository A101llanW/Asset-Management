namespace AssetManagement.Application.Helpers
{
    public sealed class DepreciationSettings
    {
        public int LifeMonths { get; set; }

        public decimal AnnualRatePercent { get; set; }

        public string LifeSource { get; set; }

        public string RateSource { get; set; }
    }
}
