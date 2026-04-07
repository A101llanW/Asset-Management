using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IUserService
    {
        IEnumerable<UserVm> GetAll();

        UserVm GetById(string id);

        void AssignRole(string userId, int roleId);
    }
}
