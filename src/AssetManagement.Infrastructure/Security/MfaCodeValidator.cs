using System;

namespace AssetManagement.Infrastructure.Security
{
    public static class MfaCodeValidator
    {
        public static bool Validate(
            bool allowAnyCode,
            string storedCode,
            DateTime? expiryUtc,
            string submittedCode,
            DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(submittedCode))
            {
                return false;
            }

            if (allowAnyCode)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(storedCode))
            {
                return false;
            }

            if (!expiryUtc.HasValue || expiryUtc.Value < utcNow)
            {
                return false;
            }

            return string.Equals(storedCode.Trim(), submittedCode.Trim(), StringComparison.Ordinal);
        }
    }
}
