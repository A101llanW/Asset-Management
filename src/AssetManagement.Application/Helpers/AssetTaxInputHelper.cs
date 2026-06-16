using System;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Helpers
{
    public static class AssetTaxInputHelper
    {
        public const string AmountMode = "Amount";

        public const string PercentageMode = "Percentage";

        public static void ApplyTaxInput(AssetCreateVm model)
        {
            if (model == null)
            {
                return;
            }

            if (!model.TaxInputValue.HasValue || model.TaxInputValue.Value <= 0)
            {
                model.TaxAmount = 0;
                return;
            }

            if (string.Equals(model.TaxInputMode, PercentageMode, StringComparison.OrdinalIgnoreCase))
            {
                model.TaxAmount = Math.Round(
                    model.AcquisitionCost * model.TaxInputValue.Value / 100m,
                    2,
                    MidpointRounding.AwayFromZero);
                return;
            }

            model.TaxAmount = Math.Round(model.TaxInputValue.Value, 2, MidpointRounding.AwayFromZero);
        }

        public static void SeedTaxInputFromStoredAmount(AssetCreateVm model)
        {
            if (model == null)
            {
                return;
            }

            model.TaxInputMode = AmountMode;
            model.TaxInputValue = model.TaxAmount > 0 ? (decimal?)model.TaxAmount : null;
        }
    }
}
