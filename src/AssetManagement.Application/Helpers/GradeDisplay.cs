using System;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Display-only terminology: stored department names and descriptions keep "Grade" (e.g. "Grade 2", "Grade 2A").
    /// The UI shows a year as "Year 2" and a class (stream) under a year in its short form "2A"
    /// (Allan, 2026-10-29: "Year 6 has classes 6A, 6B etc"). Whole-word, case-preserving; never use the result
    /// for saving, matching or lookups. QA W5.6: a "grade" joined to a code by '-' or '_' (e.g. the code
    /// ACADEMIC-GRADE2HR) is part of a code and is never relabelled; codes and tags always show their stored value.
    /// </summary>
    public static class GradeDisplay
    {
        // "Grade 6A" / "grade 10b": a class under a year -> "6A" / "10b".
        private static readonly Regex GradeClass = new Regex(
            @"(?<![A-Za-z0-9_\-])grade[ \t\u00A0]+([0-9]{1,2}[A-Za-z])(?![A-Za-z0-9_\-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex GradeWord = new Regex(
            @"(?<![A-Za-z_\-])(grade)(s?)(?![A-Za-z_\-])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>"Grade 6" -> "Year 6"; "Grade 6A" (a class) -> "6A"; "Grades" -> "Years".</summary>
        public static string ToYear(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf("grade", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return text;
            }

            var shortClasses = GradeClass.Replace(text, m => m.Groups[1].Value);
            return GradeWord.Replace(shortClasses, m => MatchCase(m.Groups[1].Value, "year") + m.Groups[2].Value);
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