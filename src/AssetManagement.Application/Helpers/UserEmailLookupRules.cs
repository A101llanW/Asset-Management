using System;
using System.Collections.Generic;
using System.Linq;

namespace AssetManagement.Application.Helpers
{
    /// <summary>
    /// Candidate row for unscoped email resolution (no OrganizationId supplied).
    /// </summary>
    public sealed class UserEmailCandidate
    {
        public string Id { get; set; }

        public int? OrganizationId { get; set; }

        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Org-scoped email uniqueness is enforced in SQL (IX_Users_OrganizationId_Email).
    /// Unscoped single-user lookups must not pick an arbitrary tenant row when the same
    /// email exists in multiple organizations (cross-tenant collision risk).
    /// </summary>
    public static class UserEmailLookupRules
    {
        /// <summary>
        /// Resolve at most one user id when OrganizationId was not supplied.
        /// Prefer a platform account (OrganizationId null). If no platform match,
        /// return the sole tenant match; if multiple tenant matches, return null (ambiguous).
        /// </summary>
        public static string ResolveUnscopedUserId(IEnumerable<UserEmailCandidate> candidates, bool activeOnly)
        {
            if (candidates == null)
            {
                return null;
            }

            var list = candidates
                .Where(c => c != null && !string.IsNullOrWhiteSpace(c.Id))
                .Where(c => !activeOnly || c.IsActive)
                .ToList();

            if (list.Count == 0)
            {
                return null;
            }

            var platform = list
                .Where(c => !c.OrganizationId.HasValue)
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .ToList();

            if (platform.Count == 1)
            {
                return platform[0].Id;
            }

            if (platform.Count > 1)
            {
                // Multiple platform rows with same email — refuse rather than pick by Id.
                return null;
            }

            var tenants = list
                .Where(c => c.OrganizationId.HasValue)
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .ToList();

            if (tenants.Count == 1)
            {
                return tenants[0].Id;
            }

            return null;
        }

        /// <summary>
        /// True when the same email maps to more than one tenant organization
        /// (platform accounts ignored). Used for diagnostics / disambiguation UI.
        /// </summary>
        public static bool IsAmbiguousAcrossTenants(IEnumerable<UserEmailCandidate> candidates, bool activeOnly)
        {
            if (candidates == null)
            {
                return false;
            }

            var distinctOrgs = candidates
                .Where(c => c != null && c.OrganizationId.HasValue)
                .Where(c => !activeOnly || c.IsActive)
                .Select(c => c.OrganizationId.Value)
                .Distinct()
                .Count();

            return distinctOrgs > 1;
        }
    }
}
