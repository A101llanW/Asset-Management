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

        /// <summary>Deactivate (false) or reactivate (true). Guards: not yourself, never the last active Company Admin.</summary>
        void SetActive(string userId, bool isActive);

        /// <summary>Records that block a hard delete (empty = deletable).</summary>
        IList<KeyValuePair<string, int>> GetDeleteBlockers(string userId);

        /// <summary>Hard delete; throws BusinessException with guidance when blocked.</summary>
        void Delete(string userId);
    }
}
