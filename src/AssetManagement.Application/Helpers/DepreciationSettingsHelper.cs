using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class DepreciationSettingsHelper
    {
        public static void ApplyAssetOverrides(Asset entity, AssetCreateVm model, bool canManage)
        {
            if (entity == null || model == null || !canManage)
            {
                return;
            }

            entity.DepreciationLifeMonths = model.UseCustomDepreciationLife
                && model.DepreciationLifeMonths.HasValue
                && model.DepreciationLifeMonths.Value > 0
                ? model.DepreciationLifeMonths
                : null;

            entity.DepreciationRatePercent = model.UseCustomDepreciationRate
                && model.DepreciationRatePercent.HasValue
                && model.DepreciationRatePercent.Value > 0
                ? model.DepreciationRatePercent
                : null;

            if (model.SalvageValue >= 0)
            {
                entity.SalvageValue = model.SalvageValue;
            }

            if (model.DepreciationMethod != 0)
            {
                entity.DepreciationMethod = model.DepreciationMethod;
            }

            if (model.DepreciationStartDate != default(System.DateTime))
            {
                entity.DepreciationStartDate = model.DepreciationStartDate;
            }
        }

        public static AssetDepreciationSettingsVm ToViewModel(DepreciationSettings settings)
        {
            settings = settings ?? new DepreciationSettings();
            return new AssetDepreciationSettingsVm
            {
                EffectiveLifeMonths = settings.LifeMonths,
                EffectiveRatePercent = settings.AnnualRatePercent,
                LifeSource = settings.LifeSource,
                RateSource = settings.RateSource
            };
        }
    }
}
