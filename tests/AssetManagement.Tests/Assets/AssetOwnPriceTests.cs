using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Tests.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    /// <summary>
    /// Allan (2026-10-08): "If an asset has no price of its own, leave it blank (Not set) and never copy
    /// the sub-type price." Covers validation, totals, depreciation, create and the import path.
    /// </summary>
    [TestFixture]
    public class AssetOwnPriceTests
    {
        private const int SubTypeId = 41;
        private const decimal SubTypePrice = 5000m;

        [Test]
        public void Validate_AcceptsBlankAndZero_RejectsNegative()
        {
            Assert.IsNull(AssetPriceRules.Validate(null));
            Assert.IsNull(AssetPriceRules.Validate(0m));
            Assert.IsNull(AssetPriceRules.Validate(1250.50m));
            Assert.IsNotNull(AssetPriceRules.Validate(-1m));
        }

        [Test]
        public void TryParse_BlankIsNotSet_ZeroIsZero()
        {
            decimal? price;
            Assert.IsTrue(AssetPriceRules.TryParse("", out price));
            Assert.IsNull(price);
            Assert.IsTrue(AssetPriceRules.TryParse("   ", out price));
            Assert.IsNull(price);
            Assert.IsTrue(AssetPriceRules.TryParse("0", out price));
            Assert.AreEqual(0m, price);
            Assert.IsTrue(AssetPriceRules.TryParse("0.00", out price));
            Assert.AreEqual(0m, price);
            Assert.IsTrue(AssetPriceRules.TryParse("1,234.567", out price));
            Assert.AreEqual(1234.57m, price);
            Assert.IsFalse(AssetPriceRules.TryParse("abc", out price));
        }

        [Test]
        public void Format_NullShowsNotSet_NeverZero()
        {
            Assert.AreEqual("Not set", AssetPriceRules.Format(null));
            StringAssert.Contains("0.00", AssetPriceRules.Format(0m));
        }

        [Test]
        public void AssetCreateVm_DataAnnotations_AcceptBlankAndZero()
        {
            Assert.IsEmpty(ValidateCost(null));
            Assert.IsEmpty(ValidateCost(0m));
            Assert.IsNotEmpty(ValidateCost(-5m));
        }

        [Test]
        public void Summarize_ExcludesNullFromTotal_AndCountsUnpriced()
        {
            var totals = AssetPriceRules.Summarize(new decimal?[] { 100m, null, 0m, 250.25m, null });
            Assert.AreEqual(350.25m, totals.Total);
            Assert.AreEqual(3, totals.PricedCount);
            Assert.AreEqual(2, totals.UnpricedCount);
            Assert.AreEqual("2 unpriced (excluded)", AssetPriceRules.FormatUnpricedNote(totals.UnpricedCount));
            Assert.AreEqual(string.Empty, AssetPriceRules.FormatUnpricedNote(0));
        }

        [Test]
        public void Depreciation_SkipsNullCost_WithoutCrashOrFallback()
        {
            var asset = BuildAsset(1, null);
            var settings = new DepreciationSettings { LifeMonths = 36, AnnualRatePercent = 33.33m };

            var position = DepreciationCalculator.Compute(asset, settings, DateTime.UtcNow);
            Assert.AreEqual(0m, position.CurrentBookValue);
            Assert.AreEqual(0m, position.AccumulatedDepreciation);
            Assert.AreEqual(0m, DepreciationSettingsResolver.CalculateMonthlyAmount(asset, settings));

            asset.DepreciationMethod = DepreciationMethod.ReducingBalance;
            position = DepreciationCalculator.Compute(asset, settings, DateTime.UtcNow);
            Assert.AreEqual(0m, position.CurrentBookValue);
            Assert.AreEqual(0m, DepreciationSettingsResolver.CalculateMonthlyAmount(asset, settings));

            DepreciationCalculator.ApplyToAsset(asset, settings, DateTime.UtcNow);
            Assert.IsNull(asset.AcquisitionCost, "Depreciation must not write a price into an unpriced asset.");
        }

        [Test]
        public void Depreciation_StillWorksForPricedAndZeroCost()
        {
            var settings = new DepreciationSettings { LifeMonths = 12, AnnualRatePercent = 100m };
            var priced = BuildAsset(2, 1200m);
            priced.DepreciationStartDate = DateTime.UtcNow.AddMonths(-6);
            var position = DepreciationCalculator.Compute(priced, settings, DateTime.UtcNow);
            Assert.AreEqual(600m, position.AccumulatedDepreciation);
            Assert.AreEqual(600m, position.CurrentBookValue);

            var zero = BuildAsset(3, 0m);
            zero.DepreciationStartDate = DateTime.UtcNow.AddMonths(-6);
            position = DepreciationCalculator.Compute(zero, settings, DateTime.UtcNow);
            Assert.AreEqual(0m, position.CurrentBookValue);
            Assert.AreEqual(0m, position.AccumulatedDepreciation);
        }

        [Test]
        public void Create_WithoutPrice_StaysNotSet_EvenWhenSubTypeHasPrice()
        {
            var unitOfWork = SeedCatalog();
            var service = TestServiceFactory.CreateAssetService(unitOfWork);

            var id = service.Create(BuildCreateVm(null));

            var created = unitOfWork.Repository<Asset>().GetById(id);
            Assert.IsNull(created.AcquisitionCost, "Sub-type price must never be copied into a new asset.");
            Assert.AreEqual(0m, created.CurrentBookValue);
        }

        [Test]
        public void Create_WithExplicitZero_SavesZero()
        {
            var unitOfWork = SeedCatalog();
            var service = TestServiceFactory.CreateAssetService(unitOfWork);

            var id = service.Create(BuildCreateVm(0m));

            Assert.AreEqual(0m, unitOfWork.Repository<Asset>().GetById(id).AcquisitionCost);
        }

        [Test]
        public void Create_RejectsNegativePrice()
        {
            var unitOfWork = SeedCatalog();
            var service = TestServiceFactory.CreateAssetService(unitOfWork);
            Assert.Throws<BusinessException>(() => service.Create(BuildCreateVm(-1m)));
        }

        [Test]
        public void SetAcquisitionCost_ClearsToNotSet_AndAllowsZero()
        {
            var unitOfWork = SeedCatalog();
            unitOfWork.Seed(BuildAsset(10, 750m));
            var service = TestServiceFactory.CreateAssetService(unitOfWork);

            service.SetAcquisitionCost(10, null, "test-user");
            var asset = unitOfWork.Repository<Asset>().GetById(10);
            Assert.IsNull(asset.AcquisitionCost);
            Assert.AreEqual(0m, asset.CurrentBookValue);

            service.SetAcquisitionCost(10, 0m, "test-user");
            Assert.AreEqual(0m, unitOfWork.Repository<Asset>().GetById(10).AcquisitionCost);

            service.SetAcquisitionCost(10, 1999.999m, "test-user");
            Assert.AreEqual(2000.00m, unitOfWork.Repository<Asset>().GetById(10).AcquisitionCost);

            Assert.Throws<BusinessException>(() => service.SetAcquisitionCost(10, -3m, "test-user"));
        }

        [Test]
        public void TotalCostOfOwnership_ExcludesUnpricedCost()
        {
            var unitOfWork = SeedCatalog();
            var asset = BuildAsset(11, null);
            asset.TaxAmount = 16m;
            unitOfWork.Seed(asset);
            var service = TestServiceFactory.CreateAssetService(unitOfWork);

            var tco = service.GetTotalCostOfOwnership(11);
            Assert.IsNull(tco.AcquisitionCost);
            Assert.AreEqual(16m, tco.TotalCostOfOwnership);
        }

        [Test]
        public void ImportPath_BlankCost_IsNotSet_NeverSubTypePriceOrPlaceholder()
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "AssetName", "Projector" },
                { "AssetSubType", "Epson - EB-X41" },
                { "AcquisitionCost", "" }
            };
            Assert.IsNull(AssetImportService.ResolveImportAcquisitionCost(row));

            row.Remove("AcquisitionCost");
            Assert.IsNull(AssetImportService.ResolveImportAcquisitionCost(row));
        }

        [Test]
        public void ImportPath_ExplicitCosts_AreKept()
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { { "AcquisitionCost", "0" } };
            Assert.AreEqual(0m, AssetImportService.ResolveImportAcquisitionCost(row));
            row["AcquisitionCost"] = "45000.50";
            Assert.AreEqual(45000.50m, AssetImportService.ResolveImportAcquisitionCost(row));
            row["AcquisitionCost"] = "-1";
            Assert.Throws<BusinessException>(() => AssetImportService.ResolveImportAcquisitionCost(row));
        }

        [Test]
        public void ImportTemplate_DoesNotRequireAcquisitionCost()
        {
            var instructions = string.Join("\n", AssetImportTemplateInstructions.Lines);
            StringAssert.Contains("AcquisitionCost (optional)", instructions);
            StringAssert.Contains("never copied", instructions);
        }

        private static IList<System.ComponentModel.DataAnnotations.ValidationResult> ValidateCost(decimal? cost)
        {
            var vm = BuildCreateVm(cost);
            vm.AssetTag = "TAG-1";
            var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            System.ComponentModel.DataAnnotations.Validator.TryValidateProperty(
                vm.AcquisitionCost,
                new System.ComponentModel.DataAnnotations.ValidationContext(vm, null, null) { MemberName = "AcquisitionCost" },
                results);
            return results;
        }

        private static FakeUnitOfWork SeedCatalog()
        {
            var unitOfWork = new FakeUnitOfWork();
            unitOfWork.Seed(new AssetCategory { Id = 1, Name = "IT Equipment", CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new AssetType { Id = 1, Name = "Projector", AssetCategoryId = 1, CreatedAt = DateTime.UtcNow, IsActive = true });
            unitOfWork.Seed(new AssetSubType
            {
                Id = SubTypeId,
                AssetTypeId = 1,
                Name = "Epson - EB-X41",
                Brand = "Epson",
                Model = "EB-X41",
                DefaultAcquisitionCost = SubTypePrice,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });
            return unitOfWork;
        }

        private static AssetCreateVm BuildCreateVm(decimal? cost)
        {
            return new AssetCreateVm
            {
                AssetName = "Projector",
                CategoryId = 1,
                AssetTypeId = 1,
                AssetSubTypeId = SubTypeId,
                PurchaseDate = DateTime.UtcNow,
                AcquisitionCost = cost,
                Currency = "KES",
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow
            };
        }

        private static Asset BuildAsset(int id, decimal? cost)
        {
            return new Asset
            {
                Id = id,
                AssetTag = "PRJ-" + id,
                AssetName = "Projector " + id,
                CategoryId = 1,
                AssetTypeId = 1,
                AssetSubTypeId = SubTypeId,
                Currency = "KES",
                AcquisitionCost = cost,
                CurrentBookValue = cost ?? 0m,
                CurrentStatus = AssetStatus.InStore,
                PurchaseDate = DateTime.UtcNow,
                DepreciationMethod = DepreciationMethod.StraightLine,
                DepreciationStartDate = DateTime.UtcNow.AddMonths(-3),
                UsefulLifeMonths = 36,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
