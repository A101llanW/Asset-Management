using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Single rule for "what does a user without a department see" (Allan, 2026-10-27): a signed-in member of
    /// the current organization who has no department gets organization-wide scope (the whole tenant, never
    /// another tenant) instead of an empty list, a 403 or a crash. Users placed in a department keep their
    /// department scope. No profile (anonymous, background job, a user from another tenant) stays fail-closed.
    /// The tenant boundary is unchanged: repositories and SQL queries keep filtering on OrganizationId.
    /// </summary>
    public static class DepartmentScopeRules
    {
        public static bool HasNoDepartment(int? departmentId)
        {
            return !departmentId.HasValue || departmentId.Value <= 0;
        }

        /// <summary>True when the loaded (organization-scoped) profile exists and has no department.</summary>
        public static bool IsUnassignedMember(UserVm profile)
        {
            return profile != null && HasNoDepartment(profile.DepartmentId);
        }
    }
}
