using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class DepreciationSettingsResolver
    {
        public static DepreciationSettings Resolve(IUnitOfWork unitOfWork, Asset asset)
        {
            if (unitOfWork == null || asset == null)
            {
                return new DepreciationSettings();
            }

            var assetType = unitOfWork.Repository<AssetType>().GetById(asset.AssetTypeId);
            var category = unitOfWork.Repository<AssetCategory>().GetById(asset.CategoryId);
            return Resolve(asset, assetType, category);
        }

        public static DepreciationSettings Resolve(Asset asset, AssetType assetType, AssetCategory category)
        {
            var settings = new DepreciationSettings();
            if (asset == null)
            {
                return settings;
            }

            if (asset.DepreciationLifeMonths.HasValue && asset.DepreciationLifeMonths.Value > 0)
            {
                settings.LifeMonths = asset.DepreciationLifeMonths.Value;
                settings.LifeSource = "asset";
            }
            else if (assetType != null && assetType.DepreciationLifeMonths.HasValue && assetType.DepreciationLifeMonths.Value > 0)
            {
                settings.LifeMonths = assetType.DepreciationLifeMonths.Value;
                settings.LifeSource = "asset type";
            }
            else if (category != null && category.DefaultDepreciationLifeMonths.HasValue && category.DefaultDepreciationLifeMonths.Value > 0)
            {
                settings.LifeMonths = category.DefaultDepreciationLifeMonths.Value;
                settings.LifeSource = "category";
            }
            else if (asset.UsefulLifeMonths.HasValue && asset.UsefulLifeMonths.Value > 0)
            {
                settings.LifeMonths = asset.UsefulLifeMonths.Value;
                settings.LifeSource = "asset useful life";
            }
            else if (assetType != null && assetType.UsefulLifeMonths.HasValue && assetType.UsefulLifeMonths.Value > 0)
            {
                settings.LifeMonths = assetType.UsefulLifeMonths.Value;
                settings.LifeSource = "asset type useful life";
            }
            else if (category != null && category.DefaultUsefulLifeMonths.HasValue && category.DefaultUsefulLifeMonths.Value > 0)
            {
                settings.LifeMonths = category.DefaultUsefulLifeMonths.Value;
                settings.LifeSource = "category useful life";
            }

            if (asset.DepreciationRatePercent.HasValue && asset.DepreciationRatePercent.Value > 0)
            {
                settings.AnnualRatePercent = asset.DepreciationRatePercent.Value;
                settings.RateSource = "asset";
            }
            else if (assetType != null && assetType.DepreciationRatePercent.HasValue && assetType.DepreciationRatePercent.Value > 0)
            {
                settings.AnnualRatePercent = assetType.DepreciationRatePercent.Value;
                settings.RateSource = "asset type";
            }
            else if (category != null && category.DefaultDepreciationRatePercent.HasValue && category.DefaultDepreciationRatePercent.Value > 0)
            {
                settings.AnnualRatePercent = category.DefaultDepreciationRatePercent.Value;
                settings.RateSource = "category";
            }
            else if (settings.LifeMonths > 0)
            {
                settings.AnnualRatePercent = System.Math.Round(1200m / settings.LifeMonths, 2);
                settings.RateSource = "derived from life";
            }

            return settings;
        }

        public static decimal CalculateMonthlyAmount(Asset asset, DepreciationSettings settings)
        {
            if (asset == null || settings == null || settings.LifeMonths <= 0 || settings.AnnualRatePercent <= 0)
            {
                return 0m;
            }

            if (asset.DepreciationMethod == Domain.Enums.DepreciationMethod.ReducingBalance)
            {
                return System.Math.Round(asset.CurrentBookValue * settings.AnnualRatePercent / 1200m, 2);
            }

            var depreciableAmount = asset.AcquisitionCost - asset.SalvageValue;
            if (depreciableAmount <= 0)
            {
                return 0m;
            }

            return System.Math.Round(depreciableAmount * settings.AnnualRatePercent / 1200m, 2);
        }
    }
}
