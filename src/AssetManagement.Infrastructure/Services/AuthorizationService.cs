using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Infrastructure.Services
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly IUnitOfWork _unitOfWork;

        public AuthorizationService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public bool HasPermission(string userId, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(permissionCode))
            {
                return false;
            }

            var user = _unitOfWork.Repository<ApplicationUser>().GetById(userId);
            if (user == null || !user.IsActive || !user.RoleId.HasValue)
            {
                return false;
            }

            var role = _unitOfWork.Repository<Role>().GetById(user.RoleId.Value);
            if (role == null || !role.IsActive)
            {
                return false;
            }

            if (role.IsSystemRole && role.Name == "Super Admin")
            {
                return true;
            }

            return _unitOfWork.Repository<RolePermission>().Find(x => x.RoleId == user.RoleId.Value)
                .Join(
                    _unitOfWork.Repository<Permission>().Find(x => x.Code == permissionCode),
                    rp => rp.PermissionId,
                    p => p.Id,
                    (rp, p) => p)
                .Any();
        }
    }
}
