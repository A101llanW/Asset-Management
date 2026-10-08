using System;
using System.Collections.Generic;
using System.Globalization;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Per-asset price rules (Allan, 2026-10-08): "If an asset has no price of its own, leave it blank
    /// (Not set) and never copy the sub-type price." A null AcquisitionCost means "Not set": it is never
    /// filled from the sub-type/type default, never treated as 0 or as the sub-type price, and it is left
    /// out of value totals (counted as unpriced instead). An explicit 0.00 is a real, saved price.
    /// </summary>
    public static class AssetPriceRules
    {
        public const string NotSetLabel = "Not set";

        public const decimal MaxPrice = 999999999m;

        /// <summary>Formats an asset's own price, or "Not set" when it has none.</summary>
        public static string Format(decimal? price)
        {
            return price.HasValue ? CurrencyFormatter.Format(price.Value) : NotSetLabel;
        }

        /// <summary>
        /// Validates an asset's own price. Blank (null) and 0.00 are both allowed; negatives and values over
        /// <see cref="MaxPrice"/> are not. Returns null when valid, otherwise a user-facing message.
        /// </summary>
        public static string Validate(decimal? price)
        {
            if (!price.HasValue)
            {
                return null;
            }

            if (price.Value < 0m)
            {
                return "Acquisition cost cannot be negative. Leave it blank for \"Not set\".";
            }

            if (price.Value > MaxPrice)
            {
                return "Acquisition cost is too large.";
            }

            return null;
        }

        /// <summary>Rounds a supplied price to 2 dp; null stays null (no default is ever substituted).</summary>
        public static decimal? Normalize(decimal? price)
        {
            return price.HasValue ? Math.Round(price.Value, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        }

        /// <summary>
        /// Parses a price typed by a user or read from an import cell. Blank means "Not set" (null) and
        /// is never replaced by a sub-type, type or placeholder price. Returns false when the text is not a number.
        /// </summary>
        public static bool TryParse(string text, out decimal? price)
        {
            price = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var trimmed = text.Trim();
            decimal parsed;
            if (decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out parsed)
                || decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
            {
                price = Normalize(parsed);
                return true;
            }

            return false;
        }

        /// <summary>Book value for an asset with this price before depreciation: 0 when unpriced.</summary>
        public static decimal OpeningBookValue(decimal? price)
        {
            return price ?? 0m;
        }

        /// <summary>Sums only priced values; unpriced (null) values are counted, never added as 0 or a default.</summary>
        public static PriceTotals Summarize(IEnumerable<decimal?> prices)
        {
            var totals = new PriceTotals();
            if (prices == null)
            {
                return totals;
            }

            foreach (var price in prices)
            {
                if (price.HasValue)
                {
                    totals.PricedCount++;
                    totals.Total += price.Value;
                }
                else
                {
                    totals.UnpricedCount++;
                }
            }

            return totals;
        }

        /// <summary>"N unpriced" note for totals, or empty when every asset is priced.</summary>
        public static string FormatUnpricedNote(int unpricedCount)
        {
            if (unpricedCount <= 0)
            {
                return string.Empty;
            }

            return unpricedCount.ToString("N0", CultureInfo.InvariantCulture) + " unpriced (excluded)";
        }

        public sealed class PriceTotals
        {
            public decimal Total { get; set; }

            public int PricedCount { get; set; }

            public int UnpricedCount { get; set; }

            public void Add(decimal? price)
            {
                if (price.HasValue)
                {
                    PricedCount++;
                    Total += price.Value;
                }
                else
                {
                    UnpricedCount++;
                }
            }
        }
    }
}
