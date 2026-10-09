using System;
using System.Collections.Generic;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Infrastructure.Security
{
    /// <summary>
    /// rel1009i: a deactivated user is told the account is deactivated ONLY when the password verifies (no account
    /// enumeration). A wrong password keeps the generic failure message and counts towards lockout as before.
    /// </summary>
    public static class InactiveAccountLoginRules
    {
        public const string LoginDeniedInactiveEventType = "LOGIN_DENIED_INACTIVE";

        /// <summary>Returns the first INACTIVE candidate whose password hash verifies, or null.</summary>
        public static ApplicationUser FindVerifiedInactive(IEnumerable<ApplicationUser> candidates, string password)
        {
            if (candidates == null || string.IsNullOrEmpty(password))
            {
                return null;
            }

            foreach (var user in candidates)
            {
                if (user == null || user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
                {
                    continue;
                }

                PasswordVerificationResult result;
                try
                {
                    result = PasswordHasher.VerifyHashedPassword(user.PasswordHash, password);
                }
                catch (FormatException)
                {
                    continue;
                }

                if (result != PasswordVerificationResult.Failed)
                {
                    return user;
                }
            }

            return null;
        }
    }
}
