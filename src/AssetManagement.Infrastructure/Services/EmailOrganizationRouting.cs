using System;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Infrastructure.Services
{
    /// <summary>
    /// Which organization's SMTP an email to a user belongs to: the user's organization, or null
    /// (platform default) for platform admins and users without an organization.
    /// </summary>
    internal static class EmailOrganizationRouting
    {
        public static int? ForUser(UserAccountRepository users, ApplicationUser user)
        {
            if (user == null || !user.OrganizationId.HasValue)
            {
                return null;
            }

            try
            {
                string roleName;
                bool isSystemRole;
                if (users != null
                    && users.TryGetRoleInfoByUserId(user.Id, out roleName, out isSystemRole)
                    && SystemRoleNames.IsPlatformAdmin(roleName, isSystemRole))
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("Email routing role lookup failed: " + ex.Message);
            }

            return user.OrganizationId;
        }
    }
}
