using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IUserService
    {
        IEnumerable<UserVm> GetAll();

        PagedListVm<UserVm> GetListPage(
            UserListFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize);

        UserVm GetById(string id);

        void AssignRole(string userId, int roleId);

        void AssignDepartment(string userId, int? departmentId);
    }
}
