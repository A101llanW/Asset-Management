using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using AssetManagement.Application.DTOs;

namespace AssetManagement.Application.Helpers
{
    public static class ImportQuantityParser
    {
        private static readonly Regex LeadingQuantityPattern = new Regex(
            @"^\s*(?<qty>\d+)\s+(?<rest>.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex TrailingUnitsPattern = new Regex(
            @"(?<qty>\d+)\s+units?\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static int ResolveQuantity(
            IDictionary<string, string> row,
            Func<IDictionary<string, string>, string, string> getValue)
        {
            if (getValue == null)
            {
                throw new ArgumentNullException("getValue");
            }

            var explicitQuantity = getValue(row, "Quantity");
            if (!string.IsNullOrWhiteSpace(explicitQuantity))
            {
                int parsed;
                if (!int.TryParse(explicitQuantity.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                    || parsed < 1)
                {
                    throw new BusinessException("Quantity must be a whole number of at least 1.");
                }

                return parsed;
            }

            var description = getValue(row, "Description");
            return ParseFromDescription(description);
        }

        public static int ParseFromDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return 1;
            }

            var trimmed = description.Trim();
            var match = LeadingQuantityPattern.Match(trimmed);
            if (match.Success)
            {
                return ParseQuantityGroup(match.Groups["qty"].Value);
            }

            match = TrailingUnitsPattern.Match(trimmed);
            if (match.Success)
            {
                return ParseQuantityGroup(match.Groups["qty"].Value);
            }

            return 1;
        }

        private static int ParseQuantityGroup(string rawQuantity)
        {
            int parsed;
            if (!int.TryParse(rawQuantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                || parsed < 1)
            {
                return 1;
            }

            return parsed;
        }
    }
}
