using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.Helpers
{
    public static class ListRoleDefaultsHelper
    {
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
                || filter.Status.HasValue
                || filter.CategoryId.HasValue
                || !string.IsNullOrWhiteSpace(filter.Search)
                || !string.IsNullOrWhiteSpace(filter.CustodianUserId);

            if (hasExplicitFilter || user == null)
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

            if (hasExplicitFilter || user == null)
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
