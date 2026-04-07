using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;

        public UserService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public IEnumerable<UserVm> GetAll()
        {
            var roles = _unitOfWork.Repository<Domain.Entities.Role>().GetAll().ToDictionary(x => x.Id, x => x.Name);
            return _unitOfWork.Repository<ApplicationUser>().GetAll()
                .OrderBy(x => x.FirstName)
                .ThenBy(x => x.LastName)
                .Select(x => new UserVm
                {
                    Id = x.Id,
                    EmployeeNumber = x.EmployeeNumber,
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    Email = x.Email,
                    Phone = x.Phone,
                    DepartmentId = x.DepartmentId,
                    PositionTitle = x.PositionTitle,
                    IsActive = x.IsActive,
                    RoleId = x.RoleId,
                    RoleName = x.RoleId.HasValue && roles.ContainsKey(x.RoleId.Value) ? roles[x.RoleId.Value] : null
                })
                .ToList();
        }

        public UserVm GetById(string id)
        {
            var user = _unitOfWork.Repository<ApplicationUser>().GetById(id);
            if (user == null)
            {
                return null;
            }

            return new UserVm
            {
                Id = user.Id,
                EmployeeNumber = user.EmployeeNumber,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                DepartmentId = user.DepartmentId,
                PositionTitle = user.PositionTitle,
                IsActive = user.IsActive,
                RoleId = user.RoleId
            };
        }

        public void AssignRole(string userId, int roleId)
        {
            var user = _unitOfWork.Repository<ApplicationUser>().GetById(userId);
            if (user == null)
            {
                return;
            }

            user.RoleId = roleId;
            user.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Repository<ApplicationUser>().Update(user);
            _unitOfWork.SaveChanges();
        }
    }
}
