using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;

namespace AssetManagement.Infrastructure.Services
{
    public class AuthorizationService : IAuthorizationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly UserAccountRepository _users;

        public AuthorizationService(
            IUnitOfWork unitOfWork,
            IOrganizationScopeService organizationScope,
            ISqlConnectionFactory connectionFactory)
        {
            _unitOfWork = unitOfWork;
            _organizationScope = organizationScope;
            _users = new UserAccountRepository(connectionFactory);
        }

        public bool HasPermission(string userId, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(permissionCode))
            {
                return false;
            }

            var user = _users.FindById(userId);
            if (user == null || !user.IsActive || !user.RoleId.HasValue)
            {
                return false;
            }

            var roleName = _users.FindRoleNameByUserId(userId);
            if (string.IsNullOrWhiteSpace(roleName))
            {
                return false;
            }

            var isPlatformAdmin = string.Equals(roleName, "Platform Admin", StringComparison.OrdinalIgnoreCase);
            if (isPlatformAdmin)
            {
                if (permissionCode.StartsWith("Platform."))
                {
                    return true;
                }

                if (_organizationScope.IsImpersonating())
                {
                    return true;
                }
            }

            return HasTenantPermission(user.RoleId.Value, roleName, permissionCode);
        }

        private bool HasTenantPermission(int roleId, string roleName, string permissionCode)
        {
            if (permissionCode.StartsWith("Platform."))
            {
                return false;
            }

            if (string.Equals(roleName, "Company Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var role = _unitOfWork.Repository<Role>().GetById(roleId);
            if (role == null || !role.IsActive)
            {
                return false;
            }

            if (role.IsSystemRole && string.Equals(role.Name, "Company Admin", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return _unitOfWork.Repository<RolePermission>().Find(x => x.RoleId == role.Id)
                .Join(
                    _unitOfWork.Repository<Permission>().Find(x => x.Code == permissionCode),
                    rp => rp.PermissionId,
                    p => p.Id,
                    (rp, p) => p)
                .Any();
        }
    }
}
