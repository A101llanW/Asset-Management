using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Helpers
{
    public static class DepreciationCalculator
    {
        public sealed class Position
        {
            public decimal AccumulatedDepreciation { get; set; }

            public decimal CurrentBookValue { get; set; }

            public int ElapsedMonths { get; set; }
        }

        public static Position Compute(Asset asset, DepreciationSettings settings, DateTime asOfUtc)
        {
            if (asset == null)
            {
                return new Position();
            }

            if (asset.CurrentStatus == AssetStatus.Disposed || asset.CurrentStatus == AssetStatus.Retired)
            {
                return new Position
                {
                    CurrentBookValue = asset.CurrentBookValue,
                    AccumulatedDepreciation = asset.AccumulatedDepreciation,
                    ElapsedMonths = 0
                };
            }

            if (settings == null || settings.LifeMonths <= 0 || settings.AnnualRatePercent <= 0)
            {
                return new Position
                {
                    CurrentBookValue = asset.AcquisitionCost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = 0
                };
            }

            if (asOfUtc.Date < asset.DepreciationStartDate.Date)
            {
                return new Position
                {
                    CurrentBookValue = asset.AcquisitionCost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = 0
                };
            }

            var elapsedMonths = CountElapsedMonths(asset.DepreciationStartDate, asOfUtc, settings.LifeMonths);
            if (elapsedMonths <= 0)
            {
                return new Position
                {
                    CurrentBookValue = asset.AcquisitionCost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = 0
                };
            }

            if (asset.DepreciationMethod == DepreciationMethod.ReducingBalance)
            {
                return ComputeReducingBalance(asset, settings, elapsedMonths);
            }

            return ComputeStraightLine(asset, settings, elapsedMonths);
        }

        public static void ApplyToAsset(Asset asset, DepreciationSettings settings, DateTime asOfUtc)
        {
            if (asset == null)
            {
                return;
            }

            var position = Compute(asset, settings, asOfUtc);
            asset.CurrentBookValue = position.CurrentBookValue;
            asset.AccumulatedDepreciation = position.AccumulatedDepreciation;
        }

        public static void RefreshOrganization(IUnitOfWork unitOfWork, int organizationId, DateTime asOfUtc)
        {
            if (unitOfWork == null)
            {
                return;
            }

            var assets = unitOfWork.Repository<Asset>()
                .Find(x => x.IsActive && x.OrganizationId == organizationId)
                .ToList();
            if (assets.Count == 0)
            {
                return;
            }

            var types = unitOfWork.Repository<AssetType>().GetAll().ToDictionary(x => x.Id);
            var categories = unitOfWork.Repository<AssetCategory>().GetAll().ToDictionary(x => x.Id);
            var changed = false;

            foreach (var asset in assets)
            {
                AssetType assetType;
                types.TryGetValue(asset.AssetTypeId, out assetType);
                AssetCategory category;
                categories.TryGetValue(asset.CategoryId, out category);
                var settings = DepreciationSettingsResolver.Resolve(asset, assetType, category);
                var beforeBook = asset.CurrentBookValue;
                var beforeAccumulated = asset.AccumulatedDepreciation;
                ApplyToAsset(asset, settings, asOfUtc);
                if (asset.CurrentBookValue == beforeBook && asset.AccumulatedDepreciation == beforeAccumulated)
                {
                    continue;
                }

                asset.UpdatedAt = asOfUtc;
                unitOfWork.Repository<Asset>().Update(asset);
                changed = true;
            }

            if (changed)
            {
                unitOfWork.SaveChanges();
            }
        }

        private static int CountElapsedMonths(DateTime startDate, DateTime asOfUtc, int lifeMonths)
        {
            var start = new DateTime(startDate.Year, startDate.Month, 1);
            var asOf = new DateTime(asOfUtc.Year, asOfUtc.Month, 1);
            var months = (asOf.Year - start.Year) * 12 + (asOf.Month - start.Month);
            if (months < 0)
            {
                months = 0;
            }

            return Math.Min(months, lifeMonths);
        }

        private static Position ComputeStraightLine(Asset asset, DepreciationSettings settings, int elapsedMonths)
        {
            var depreciable = asset.AcquisitionCost - asset.SalvageValue;
            if (depreciable <= 0m)
            {
                return new Position
                {
                    CurrentBookValue = asset.AcquisitionCost,
                    AccumulatedDepreciation = 0m,
                    ElapsedMonths = elapsedMonths
                };
            }

            var monthly = System.Math.Round(depreciable * settings.AnnualRatePercent / 1200m, 2);
            var accumulated = System.Math.Min(monthly * elapsedMonths, depreciable);
            var bookValue = asset.AcquisitionCost - accumulated;
            if (bookValue < asset.SalvageValue)
            {
                accumulated = asset.AcquisitionCost - asset.SalvageValue;
                bookValue = asset.SalvageValue;
            }

            return new Position
            {
                AccumulatedDepreciation = accumulated,
                CurrentBookValue = bookValue,
                ElapsedMonths = elapsedMonths
            };
        }

        private static Position ComputeReducingBalance(Asset asset, DepreciationSettings settings, int elapsedMonths)
        {
            var bookValue = asset.AcquisitionCost;
            var accumulated = 0m;
            var workingAsset = new Asset
            {
                DepreciationMethod = asset.DepreciationMethod,
                AcquisitionCost = asset.AcquisitionCost,
                SalvageValue = asset.SalvageValue,
                CurrentBookValue = bookValue
            };

            for (var month = 0; month < elapsedMonths; month++)
            {
                workingAsset.CurrentBookValue = bookValue;
                var monthly = DepreciationSettingsResolver.CalculateMonthlyAmount(workingAsset, settings);
                if (monthly <= 0m)
                {
                    break;
                }

                if (bookValue - monthly < asset.SalvageValue)
                {
                    monthly = bookValue - asset.SalvageValue;
                    accumulated += monthly;
                    bookValue = asset.SalvageValue;
                    break;
                }

                bookValue -= monthly;
                accumulated += monthly;
            }

            return new Position
            {
                AccumulatedDepreciation = accumulated,
                CurrentBookValue = bookValue,
                ElapsedMonths = elapsedMonths
            };
        }
    }
}
