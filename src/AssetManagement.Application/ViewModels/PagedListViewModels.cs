using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    public class PagedListVm<T>
    {
        public IList<T> Items { get; set; } = new List<T>();

        public int TotalCount { get; set; }

        public string Search { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class UserListFilterVm
    {
        public string Search { get; set; }

        public int? RoleId { get; set; }

        public int? DepartmentId { get; set; }

        public bool? IsActive { get; set; }
    }

    public class PlatformUserListFilterVm
    {
        public string Search { get; set; }

        public int? OrganizationId { get; set; }

        public string UserScope { get; set; }

        public int? RoleId { get; set; }

        public bool? IsActive { get; set; }
    }
}
