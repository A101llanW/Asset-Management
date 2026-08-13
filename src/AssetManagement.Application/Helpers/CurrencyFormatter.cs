using System.Globalization;

namespace AssetManagement.Application.Helpers
{
    public static class CurrencyFormatter
    {
        public const string CurrencyCode = FinanceDefaults.DefaultCurrencyCode;

        private static readonly CultureInfo DisplayCulture = CreateDisplayCulture();

        private static CultureInfo CreateDisplayCulture()
        {
            try
            {
                return (CultureInfo)CultureInfo.GetCultureInfo("en-KE").Clone();
            }
            catch (CultureNotFoundException)
            {
                // Linux/Mono and minimal server images may not ship en-KE; number format matches for N2.
                return (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
            }
        }

        public static string Format(decimal amount)
        {
            return Format(amount, 2);
        }

        public static string Format(decimal amount, int decimalPlaces)
        {
            if (decimalPlaces < 0)
            {
                decimalPlaces = 0;
            }

            return CurrencyCode + " " + amount.ToString("N" + decimalPlaces, DisplayCulture);
        }

        public static string Format(decimal? amount)
        {
            return amount.HasValue ? Format(amount.Value) : string.Empty;
        }

        public static string Format(decimal? amount, int decimalPlaces)
        {
            return amount.HasValue ? Format(amount.Value, decimalPlaces) : string.Empty;
        }
    }
}
