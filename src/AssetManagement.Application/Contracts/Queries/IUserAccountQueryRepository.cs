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

        UserVm GetUserById(string userId, int organizationId);

        UserDisplayProjection GetDisplayById(string userId, int? organizationId);

        int? GetRoleIdByUserId(string userId);

        int CountUsersForOrganization(int organizationId);

        IList<PlatformUserListItemVm> GetAllUsersForPlatformAdmin();

        PlatformUserListItemVm GetUserByIdForPlatform(string userId);

        IList<RoleVm> GetPlatformRoles();
    }
}
