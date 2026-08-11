using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Helpers
{
    public static class AssetTagHelper
    {
        public const int DefaultRandomTagLength = 10;

        private const string RandomTagAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private static readonly Dictionary<string, string> KnownTypeAbbreviations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Laptop", "LTP" },
                { "Desktop", "DTP" },
                { "Router", "RTR" },
                { "Printer", "PRT" },
                { "Projector", "PRJ" },
                { "Office Chair", "CHR" },
                { "Office Desk", "DESK" },
                { "Lab Microscope", "MED" },
                { "Lab Centrifuge", "MED" },
                { "Vehicle", "VHC" }
            };

        public static string ResolveDepartmentCode(Department department)
        {
            if (department == null)
            {
                return "GEN";
            }

            if (!string.IsNullOrWhiteSpace(department.Code))
            {
                return NormalizeToken(department.Code, 8);
            }

            return DeriveCodeFromName(department.Name, 8);
        }

        public static string ResolveTypeAbbreviation(string assetTypeName)
        {
            if (string.IsNullOrWhiteSpace(assetTypeName))
            {
                return "AST";
            }

            string knownAbbreviation;
            if (KnownTypeAbbreviations.TryGetValue(assetTypeName.Trim(), out knownAbbreviation))
            {
                return knownAbbreviation;
            }

            return DeriveCodeFromName(assetTypeName, 4);
        }

        public static string BuildTagPrefix(string departmentCode, string assetTypeName)
        {
            return NormalizeToken(departmentCode, 8) + "-" + ResolveTypeAbbreviation(assetTypeName);
        }

        public static string GenerateNextTag(IQueryable<Asset> activeAssets, string departmentCode, string assetTypeName)
        {
            var prefix = BuildTagPrefix(departmentCode, assetTypeName) + "-";
            var nextSequence = GetNextSequence(activeAssets, prefix);
            return prefix + nextSequence.ToString("000");
        }

        /// <summary>
        /// Generates a unique random tag that is not derived from department, type, or asset name.
        /// </summary>
        public static string GenerateUniqueRandomTag(
            IEnumerable<string> takenTags,
            ISet<string> reservedTags = null,
            int length = DefaultRandomTagLength,
            int maxAttempts = 100)
        {
            if (length < 6)
            {
                length = 6;
            }

            var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddTakenTags(taken, takenTags);
            AddTakenTags(taken, reservedTags);

            for (var attempt = 0; attempt < maxAttempts; attempt++)
            {
                var candidate = GenerateRandomToken(length);
                if (taken.Add(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Unable to generate a unique random asset tag.");
        }

        public static int GetNextSequence(IQueryable<Asset> activeAssets, string tagPrefix)
        {
            if (activeAssets == null || string.IsNullOrWhiteSpace(tagPrefix))
            {
                return 1;
            }

            var pattern = "^" + Regex.Escape(tagPrefix) + "(\\d+)$";
            var maxSequence = 0;

            foreach (var tag in activeAssets
                .Where(x => x.AssetTag != null && x.AssetTag.StartsWith(tagPrefix))
                .Select(x => x.AssetTag)
                .ToList())
            {
                var match = Regex.Match(tag, pattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    continue;
                }

                int sequence;
                if (int.TryParse(match.Groups[1].Value, out sequence) && sequence > maxSequence)
                {
                    maxSequence = sequence;
                }
            }

            return maxSequence + 1;
        }

        private static string DeriveCodeFromName(string name, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "GEN";
            }

            var words = name.Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (words.Length >= 2)
            {
                var acronym = string.Concat(words.Select(word => word[0]));
                return NormalizeToken(acronym, maxLength);
            }

            return NormalizeToken(words[0], maxLength);
        }

        private static string NormalizeToken(string value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GEN";
            }

            var cleaned = Regex.Replace(value.Trim().ToUpperInvariant(), @"[^A-Z0-9]", string.Empty);
            if (cleaned.Length == 0)
            {
                return "GEN";
            }

            return cleaned.Length <= maxLength ? cleaned : cleaned.Substring(0, maxLength);
        }

        private static void AddTakenTags(ISet<string> taken, IEnumerable<string> tags)
        {
            if (taken == null || tags == null)
            {
                return;
            }

            foreach (var tag in tags)
            {
                if (!string.IsNullOrWhiteSpace(tag))
                {
                    taken.Add(tag.Trim());
                }
            }
        }

        private static string GenerateRandomToken(int length)
        {
            var chars = new char[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                var bytes = new byte[length];
                rng.GetBytes(bytes);
                for (var i = 0; i < length; i++)
                {
                    chars[i] = RandomTagAlphabet[bytes[i] % RandomTagAlphabet.Length];
                }
            }

            return new string(chars);
        }
    }
}
