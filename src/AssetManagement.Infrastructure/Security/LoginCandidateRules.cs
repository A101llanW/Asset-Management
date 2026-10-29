using System;
using System.Collections.Generic;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Infrastructure.Security
{
    public enum LoginCandidateOutcome
    {
        /// <summary>The typed password opens no account this portal may use: generic failure.</summary>
        NoMatch = 0,

        /// <summary>The typed password opens exactly one account: sign in to it.</summary>
        SignIn = 1,

        /// <summary>Root portal only: the typed password opens several accounts; offer only those.</summary>
        Choose = 2
    }

    public sealed class LoginCandidateResolution
    {
        public LoginCandidateOutcome Outcome { get; set; }

        /// <summary>Set when Outcome is SignIn.</summary>
        public ApplicationUser User { get; set; }

        /// <summary>True when the matched hash should be upgraded after sign-in.</summary>
        public bool RehashNeeded { get; set; }

        /// <summary>Accounts the typed password opens (empty for NoMatch). Never contains an account whose
        /// own password does not match.</summary>
        public IList<ApplicationUser> Verified { get; set; }
    }

    /// <summary>
    /// 2026-10-29 (t770u): every company account is its own Users row with its own password hash, so one email can
    /// have a different password in each company. The typed password is checked against each row on its own:
    /// - company portal (/{tenant}/Account/Login): only that company's row is considered; other companies are never
    ///   checked, listed or hinted.
    /// - root portal: only rows whose own password matches are kept. One match signs in directly; several give a
    ///   chooser of exactly those; none is the same generic failure as a wrong password.
    /// </summary>
    public static class LoginCandidateRules
    {
        public static LoginCandidateResolution Resolve(IEnumerable<ApplicationUser> candidates, string password, int? portalOrganizationId)
        {
            var verified = new List<ApplicationUser>();
            var rehash = new List<bool>();
            if (candidates != null && !string.IsNullOrEmpty(password))
            {
                foreach (var user in candidates)
                {
                    if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
                    {
                        continue;
                    }

                    if (portalOrganizationId.HasValue
                        && (!user.OrganizationId.HasValue || user.OrganizationId.Value != portalOrganizationId.Value))
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

                    if (result == PasswordVerificationResult.Failed)
                    {
                        continue;
                    }

                    verified.Add(user);
                    rehash.Add(result == PasswordVerificationResult.SuccessRehashNeeded);
                }
            }

            if (verified.Count == 0)
            {
                return new LoginCandidateResolution { Outcome = LoginCandidateOutcome.NoMatch, Verified = verified };
            }

            if (verified.Count == 1)
            {
                return new LoginCandidateResolution
                {
                    Outcome = LoginCandidateOutcome.SignIn,
                    User = verified[0],
                    RehashNeeded = rehash[0],
                    Verified = verified
                };
            }

            if (portalOrganizationId.HasValue)
            {
                // Unique index (OrganizationId, Email) makes this unreachable; never show a chooser on a company portal.
                return new LoginCandidateResolution
                {
                    Outcome = LoginCandidateOutcome.SignIn,
                    User = verified[0],
                    RehashNeeded = rehash[0],
                    Verified = new List<ApplicationUser> { verified[0] }
                };
            }

            return new LoginCandidateResolution { Outcome = LoginCandidateOutcome.Choose, Verified = verified };
        }
    }
}