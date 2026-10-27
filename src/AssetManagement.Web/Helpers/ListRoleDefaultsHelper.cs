using AssetManagement.Application.Security;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.Helpers
{
    public static class ListRoleDefaultsHelper
    {
        /// <summary>
        /// 2026-10-27 (QA, no-department fix): a tenant member with no department has organization-wide scope
        /// (DepartmentScopeRules, same rule as DepartmentScopeService.IsUnassignedOrganizationWide), so the list opens on
        /// the whole organization. Before, the fallback was "in my custody", which is usually empty, so the default
        /// Assets and Assignments lists showed 'No assets yet'. Users with Assets.ViewAll already skip the defaults
        /// (isSuperAdmin = BaseController.HasOrgWideScope). Users in a department keep their current default.
        /// The tenant boundary is unchanged: the query services still filter on OrganizationId.
        /// </summary>
        public static bool HasOrganizationWideDefault(UserVm user)
        {
            return DepartmentScopeRules.IsUnassignedMember(user);
        }

        public static AssetFilterVm ApplyAssetListDefaults(AssetFilterVm filter, UserVm user, bool canApproveOthers, bool isSuperAdmin)
        {
            if (filter == null)
            {
                filter = new AssetFilterVm();
            }

            if (isSuperAdmin)
            {
                return filter;
            }

            var hasExplicitFilter = filter.DepartmentId.HasValue
                || filter.Section.HasValue
                || filter.Status.HasValue
                || filter.CategoryId.HasValue
                || !string.IsNullOrWhiteSpace(filter.Search)
                || !string.IsNullOrWhiteSpace(filter.CustodianUserId)
                || filter.UnassignedOnly
                || filter.MissingSubTypeOnly;

            if (hasExplicitFilter || user == null || HasOrganizationWideDefault(user))
            {
                return filter;
            }

            if (canApproveOthers && user.DepartmentId.HasValue)
            {
                filter.DepartmentId = user.DepartmentId;
            }
            else
            {
                filter.CustodianUserId = user.Id;
            }

            return filter;
        }

        public static AssignmentFilterVm ApplyAssignmentListDefaults(AssignmentFilterVm filter, UserVm user, bool canApproveOthers, bool isSuperAdmin)
        {
            if (filter == null)
            {
                filter = new AssignmentFilterVm();
            }

            if (isSuperAdmin)
            {
                return filter;
            }

            var hasExplicitFilter = filter.DepartmentId.HasValue
                || !string.IsNullOrWhiteSpace(filter.Search)
                || !string.IsNullOrWhiteSpace(filter.CustodianUserId)
                || filter.ActiveOnly.HasValue;

            if (hasExplicitFilter || user == null || HasOrganizationWideDefault(user))
            {
                return filter;
            }

            if (canApproveOthers && user.DepartmentId.HasValue)
            {
                filter.DepartmentId = user.DepartmentId;
            }
            else
            {
                filter.CustodianUserId = user.Id;
                filter.ActiveOnly = true;
            }

            return filter;
        }

        public static AssetRequestFilterVm ApplyAssetRequestListDefaults(AssetRequestFilterVm filter, UserVm user, bool canApprove, bool isSuperAdmin)
        {
            if (filter == null)
            {
                filter = new AssetRequestFilterVm();
            }

            if (isSuperAdmin || canApprove)
            {
                return filter;
            }

            if (user != null && string.IsNullOrWhiteSpace(filter.RequestedById))
            {
                filter.RequestedById = user.Id;
            }

            return filter;
        }
    }
}
