using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Security;
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

            var role = _unitOfWork.Repository<Role>().GetById(user.RoleId.Value);
            if (role == null || !role.IsActive)
            {
                return false;
            }

            // Platform Admin: require IsSystemRole so a renamed custom role cannot escalate.
            if (SystemRoleNames.IsPlatformAdmin(role.Name, role.IsSystemRole))
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

            return HasTenantPermission(role, permissionCode);
        }

        private bool HasTenantPermission(Role role, string permissionCode)
        {
            if (permissionCode.StartsWith("Platform."))
            {
                return false;
            }

            // Company Admin bypass: IsSystemRole required (name-only grant removed in Phase B).
            if (SystemRoleNames.IsCompanyAdmin(role.Name, role.IsSystemRole))
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
