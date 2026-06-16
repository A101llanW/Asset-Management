using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class AssetTaxInputHelperTests
    {
        [Test]
        public void ApplyTaxInput_UsesFixedAmountWhenModeIsAmount()
        {
            var model = new AssetCreateVm
            {
                AcquisitionCost = 1000m,
                TaxInputMode = AssetTaxInputHelper.AmountMode,
                TaxInputValue = 160m
            };

            AssetTaxInputHelper.ApplyTaxInput(model);

            Assert.AreEqual(160m, model.TaxAmount);
        }

        [Test]
        public void ApplyTaxInput_CalculatesPercentageOfAcquisitionCost()
        {
            var model = new AssetCreateVm
            {
                AcquisitionCost = 1000m,
                TaxInputMode = AssetTaxInputHelper.PercentageMode,
                TaxInputValue = 16m
            };

            AssetTaxInputHelper.ApplyTaxInput(model);

            Assert.AreEqual(160m, model.TaxAmount);
        }

        [Test]
        public void ApplyTaxInput_ClearsTaxWhenInputIsBlank()
        {
            var model = new AssetCreateVm
            {
                AcquisitionCost = 1000m,
                TaxAmount = 99m,
                TaxInputMode = AssetTaxInputHelper.AmountMode,
                TaxInputValue = null
            };

            AssetTaxInputHelper.ApplyTaxInput(model);

            Assert.AreEqual(0m, model.TaxAmount);
        }

        [Test]
        public void SeedTaxInputFromStoredAmount_UsesAmountModeForExistingTax()
        {
            var model = new AssetCreateVm
            {
                AcquisitionCost = 1000m,
                TaxAmount = 75m
            };

            AssetTaxInputHelper.SeedTaxInputFromStoredAmount(model);

            Assert.AreEqual(AssetTaxInputHelper.AmountMode, model.TaxInputMode);
            Assert.AreEqual(75m, model.TaxInputValue);
        }
    }
}
