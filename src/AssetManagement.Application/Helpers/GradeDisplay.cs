using System;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Display-only terminology: stored department names and descriptions keep "Grade" (e.g. "Grade 2A"),
    /// the UI shows "Year" ("Year 2A"). Whole-word, case-preserving; never use the result for saving,
    /// matching or lookups.
    /// </summary>
    public static class GradeDisplay
    {
        private static readonly Regex GradeWord = new Regex(
            @"(?<![A-Za-z])(grade)(s?)(?![A-Za-z])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        public static string ToYear(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("grade", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return text;
            }

            return GradeWord.Replace(text, m => MatchCase(m.Groups[1].Value, "year") + m.Groups[2].Value);
        }

        private static string MatchCase(string source, string replacement)
        {
            if (source.ToUpperInvariant() == source)
            {
                return replacement.ToUpperInvariant();
            }

            if (char.IsUpper(source[0]))
            {
                return char.ToUpperInvariant(replacement[0]) + replacement.Substring(1);
            }

            return replacement;
        }
    }
}