using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.ViewModels.Platform;

namespace AssetManagement.Application.Contracts.Queries
{
    public class UserDisplayProjection
    {
        public string Id { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public int? RoleId { get; set; }

        public string DisplayName { get; set; }
    }

    public interface IUserAccountQueryRepository
    {
        IList<UserVm> GetUsersForOrganization(int organizationId, int? departmentId, bool bypassDepartmentScope);

        PagedListVm<UserVm> GetUserListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            UserListFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize);

        UserVm GetUserById(string userId, int organizationId);

        UserDisplayProjection GetDisplayById(string userId, int? organizationId);

        int? GetRoleIdByUserId(string userId);

        int CountUsersForOrganization(int organizationId);

        int CountUsersForRole(int organizationId, int roleId);

        int CountActiveUsersForDepartment(int organizationId, int departmentId);

        IList<PlatformUserListItemVm> GetAllUsersForPlatformAdmin();

        PagedListVm<PlatformUserListItemVm> GetPlatformUserListPage(
            PlatformUserListFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize);

        PlatformUserIndexViewModel GetPlatformUserIndexPage(
            PlatformUserListFilterVm filter,
            string sort,
            string direction,
            string category,
            int page,
            int pageSize);

        PlatformUserListItemVm GetUserByIdForPlatform(string userId);

        IList<RoleVm> GetPlatformRoles();
    }
}
