using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels.Platform
{
    public class PlatformUserListItemVm : UserVm
    {
        public int? OrganizationId { get; set; }

        public string OrganizationName { get; set; }

        public bool IsSystemUser
        {
            get { return !OrganizationId.HasValue; }
        }

        public bool IsOrganizationAdmin
        {
            get
            {
                return OrganizationId.HasValue
                    && string.Equals(RoleName, "Company Admin", System.StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    public class PlatformUserOrganizationGroupVm
    {
        public int OrganizationId { get; set; }

        public string OrganizationName { get; set; }

        public IList<PlatformUserListItemVm> Users { get; set; } = new List<PlatformUserListItemVm>();
    }

    public class PlatformUserIndexViewModel
    {
        public string Search { get; set; }

        public int? OrganizationId { get; set; }

        public string UserScope { get; set; }

        public int? RoleId { get; set; }

        public bool? IsActive { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; }

        public string Category { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 25;

        public int SystemUserCount { get; set; }

        public int OrganizationAdminCount { get; set; }

        public int OrganizationCount { get; set; }

        public int TotalCount { get; set; }

        public IList<PlatformUserListItemVm> SystemUsers { get; set; } = new List<PlatformUserListItemVm>();

        public IList<PlatformUserListItemVm> OrganizationAdmins { get; set; } = new List<PlatformUserListItemVm>();

        public IList<PlatformUserOrganizationGroupVm> OrganizationGroups { get; set; } = new List<PlatformUserOrganizationGroupVm>();

        public int ActiveTotalCount { get; set; }

        public int ActiveTotalPages { get; set; }

        public int ActiveStartItem { get; set; }

        public int ActiveEndItem { get; set; }
    }
}
