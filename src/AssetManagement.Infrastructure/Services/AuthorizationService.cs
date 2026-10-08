using System;
using System.Collections.Generic;
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

        // Registered per HTTP request: a page makes ~36+ permission checks (sidebar, buttons, filters).
        // Memoise the user row and each role's permission codes so they are read once per request
        // instead of 2-3 queries per check.
        private readonly Dictionary<string, ApplicationUser> _userMemo =
            new Dictionary<string, ApplicationUser>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<int, HashSet<string>> _rolePermissionMemo = new Dictionary<int, HashSet<string>>();

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

            var user = FindUser(userId);
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

            return GetRolePermissionCodes(role.Id).Contains(permissionCode);
        }

        private ApplicationUser FindUser(string userId)
        {
            ApplicationUser user;
            if (!_userMemo.TryGetValue(userId, out user))
            {
                user = _users.FindById(userId);
                _userMemo[userId] = user;
            }

            return user;
        }

        private HashSet<string> GetRolePermissionCodes(int roleId)
        {
            HashSet<string> codes;
            if (_rolePermissionMemo.TryGetValue(roleId, out codes))
            {
                return codes;
            }

            codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var permissionIds = _unitOfWork.Repository<RolePermission>().Find(x => x.RoleId == roleId)
                .Select(x => x.PermissionId)
                .Distinct()
                .ToList();
            if (permissionIds.Count > 0)
            {
                foreach (var permission in _unitOfWork.Repository<Permission>().Find(x => permissionIds.Contains(x.Id)))
                {
                    if (!string.IsNullOrWhiteSpace(permission.Code))
                    {
                        codes.Add(permission.Code.Trim());
                    }
                }
            }

            _rolePermissionMemo[roleId] = codes;
            return codes;
        }
    }
}
