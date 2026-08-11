using System;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class DepreciationSettingsResolverTests
    {
        [Test]
        public void Resolve_UsesAssetTypeRateOverCategory()
        {
            var settings = DepreciationSettingsResolver.Resolve(
                new Asset { DepreciationLifeMonths = null, DepreciationRatePercent = null, UsefulLifeMonths = 48 },
                new AssetType { DepreciationLifeMonths = 36, DepreciationRatePercent = 25m },
                new AssetCategory { DefaultDepreciationLifeMonths = 60, DefaultDepreciationRatePercent = 10m });

            Assert.AreEqual(36, settings.LifeMonths);
            Assert.AreEqual("asset type", settings.LifeSource);
            Assert.AreEqual(25m, settings.AnnualRatePercent);
            Assert.AreEqual("asset type", settings.RateSource);
        }

        [Test]
        public void Resolve_FallsBackToUsefulLifeWhenDepreciationLifeUnset()
        {
            var settings = DepreciationSettingsResolver.Resolve(
                new Asset { UsefulLifeMonths = 24 },
                new AssetType { UsefulLifeMonths = 36 },
                new AssetCategory { DefaultUsefulLifeMonths = 60 });

            Assert.AreEqual(24, settings.LifeMonths);
            Assert.AreEqual("asset useful life", settings.LifeSource);
            Assert.AreEqual(50m, settings.AnnualRatePercent);
            Assert.AreEqual("derived from life", settings.RateSource);
        }

        [Test]
        public void CalculateMonthlyAmount_StraightLine_UsesAnnualRate()
        {
            var asset = new Asset
            {
                DepreciationMethod = DepreciationMethod.StraightLine,
                AcquisitionCost = 1200m,
                SalvageValue = 0m,
                CurrentBookValue = 1200m
            };
            var settings = new DepreciationSettings { AnnualRatePercent = 24m, LifeMonths = 60 };

            var monthly = DepreciationSettingsResolver.CalculateMonthlyAmount(asset, settings);

            Assert.AreEqual(24m, monthly);
        }

        [Test]
        public void Compute_StraightLine_UsesElapsedMonthsAndRate()
        {
            var start = new DateTime(2024, 1, 1);
            var asset = new Asset
            {
                DepreciationMethod = DepreciationMethod.StraightLine,
                AcquisitionCost = 1200m,
                SalvageValue = 0m,
                DepreciationStartDate = start,
                CurrentStatus = AssetStatus.Assigned
            };
            var settings = new DepreciationSettings { AnnualRatePercent = 24m, LifeMonths = 60 };

            var position = DepreciationCalculator.Compute(asset, settings, new DateTime(2024, 7, 1));

            Assert.AreEqual(6, position.ElapsedMonths);
            Assert.AreEqual(144m, position.AccumulatedDepreciation);
            Assert.AreEqual(1056m, position.CurrentBookValue);
        }

        [Test]
        public void Compute_DoesNotDropBookValueBelowSalvage()
        {
            var asset = new Asset
            {
                DepreciationMethod = DepreciationMethod.StraightLine,
                AcquisitionCost = 1200m,
                SalvageValue = 100m,
                DepreciationStartDate = new DateTime(2020, 1, 1),
                CurrentStatus = AssetStatus.Assigned
            };
            var settings = new DepreciationSettings { AnnualRatePercent = 100m, LifeMonths = 12 };

            var position = DepreciationCalculator.Compute(asset, settings, new DateTime(2026, 1, 1));

            Assert.AreEqual(100m, position.CurrentBookValue);
            Assert.AreEqual(1100m, position.AccumulatedDepreciation);
        }

        [Test]
        public void ApplyToAsset_PersistsCalculatedBookValue()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new AssetCategory { Id = 1, Name = "IT", DefaultUsefulLifeMonths = 36 });
            unitOfWork.Seed(new AssetType { Id = 1, AssetCategoryId = 1, Name = "Laptop", UsefulLifeMonths = 36, DepreciationRatePercent = 20m });
            var asset = new Asset
            {
                Id = 2,
                AssetTag = "AST-002",
                AssetName = "Desktop",
                CategoryId = 1,
                AssetTypeId = 1,
                SupplierId = 1,
                DepartmentId = 1,
                Currency = "USD",
                AcquisitionCost = 1200,
                CurrentBookValue = 1200,
                SalvageValue = 0,
                CurrentStatus = AssetStatus.Assigned,
                PurchaseDate = new DateTime(2025, 1, 1),
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = new DateTime(2025, 1, 1),
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            unitOfWork.Seed(asset);

            var settings = DepreciationSettingsResolver.Resolve(asset,
                unitOfWork.Repository<AssetType>().GetById(1),
                unitOfWork.Repository<AssetCategory>().GetById(1));
            DepreciationCalculator.ApplyToAsset(asset, settings, new DateTime(2025, 7, 1));
            unitOfWork.Repository<Asset>().Update(asset);
            unitOfWork.SaveChanges();

            var updated = unitOfWork.Repository<Asset>().GetById(2);
            Assert.Less(updated.CurrentBookValue, 1200m);
            Assert.Greater(updated.AccumulatedDepreciation, 0m);
        }
    }
}
