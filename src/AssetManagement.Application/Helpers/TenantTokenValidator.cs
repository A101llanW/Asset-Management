using System;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Helpers
{
    public static class TenantTokenValidator
    {
        private static readonly Regex SlugPattern = new Regex(@"^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RecruitmentSlugPattern = new Regex(@"^[a-z][0-9]{8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AccessTokenPattern = new Regex(@"^[a-f0-9]{8}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsPlausibleToken(string token, Func<string, bool> isReservedSegment)
        {
            if (string.IsNullOrWhiteSpace(token) || (isReservedSegment != null && isReservedSegment(token)))
            {
                return false;
            }

            var normalized = token.Trim().ToLowerInvariant();
            if (RecruitmentSlugPattern.IsMatch(normalized) || AccessTokenPattern.IsMatch(normalized))
            {
                return true;
            }

            return SlugPattern.IsMatch(normalized);
        }
    }
}
