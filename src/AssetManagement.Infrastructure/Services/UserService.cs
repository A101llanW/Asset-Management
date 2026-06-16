using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;

namespace AssetManagement.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuthorizationService _authorizationService;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly ICurrentUserContext _currentUser;
        private readonly IAuditWriter _auditWriter;
        private readonly IUserAccountQueryRepository _userAccountQueryRepository;

        public UserService(
            IUnitOfWork unitOfWork,
            IAuthorizationService authorizationService,
            IOrganizationScopeService organizationScope,
            ICurrentUserContext currentUser,
            IAuditWriter auditWriter,
            IUserAccountQueryRepository userAccountQueryRepository)
        {
            _unitOfWork = unitOfWork;
            _authorizationService = authorizationService;
            _organizationScope = organizationScope;
            _currentUser = currentUser;
            _auditWriter = auditWriter;
            _userAccountQueryRepository = userAccountQueryRepository;
        }

        public IEnumerable<UserVm> GetAll()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new List<UserVm>();
            }

            return _userAccountQueryRepository.GetUsersForOrganization(
                organizationId.Value,
                null,
                true);
        }

        public UserVm GetById(string id)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return null;
            }

            return _userAccountQueryRepository.GetUserById(id, organizationId.Value);
        }

        public void AssignRole(string userId, int roleId)
        {
            var user = _unitOfWork.Writer<ApplicationUser>().GetById(userId);
            if (user == null)
            {
                throw new BusinessException("User not found.");
            }

            var role = _unitOfWork.Writer<Role>().GetById(roleId);
            if (role == null || !role.IsActive)
            {
                throw new BusinessException("Role not found.");
            }

            if (role.IsSystemRole
                && !_organizationScope.IsCompanyAdmin()
                && !_organizationScope.IsActualPlatformAdmin())
            {
                throw new BusinessException("Only Company Administrators can assign system roles.");
            }

            EnsurePermissionCeiling(roleId);

            var previousRoleId = user.RoleId;
            user.RoleId = roleId;
            user.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Writer<ApplicationUser>().Update(user);
            _unitOfWork.SaveChanges();
            _auditWriter.Write(
                "Users.AssignRole",
                nameof(ApplicationUser),
                userId,
                previousRoleId.HasValue ? previousRoleId.Value.ToString() : null,
                roleId.ToString());
        }

        public void AssignDepartment(string userId, int? departmentId)
        {
            var user = _unitOfWork.Writer<ApplicationUser>().GetById(userId);
            if (user == null)
            {
                return;
            }

            if (departmentId.HasValue)
            {
                var department = _unitOfWork.Writer<Department>().GetById(departmentId.Value);
                if (department == null)
                {
                    return;
                }
            }

            user.DepartmentId = departmentId;
            user.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Writer<ApplicationUser>().Update(user);
            _unitOfWork.SaveChanges();
        }

        private void EnsurePermissionCeiling(int roleId)
        {
            if (_organizationScope.IsCompanyAdmin() || _organizationScope.IsActualPlatformAdmin())
            {
                return;
            }

            var actorId = _currentUser == null ? null : _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new BusinessException("You must be signed in to assign roles.");
            }

            var permissionIds = _unitOfWork.Repository<RolePermission>()
                .Find(x => x.RoleId == roleId)
                .Select(x => x.PermissionId)
                .Distinct()
                .ToList();

            if (permissionIds.Count == 0)
            {
                return;
            }

            var permissions = _unitOfWork.Repository<Permission>()
                .Find(x => permissionIds.Contains(x.Id))
                .ToList();

            foreach (var permission in permissions)
            {
                if (!_authorizationService.HasPermission(actorId, permission.Code))
                {
                    throw new BusinessException(
                        "You cannot assign the role '" + _unitOfWork.Writer<Role>().GetById(roleId)?.Name
                        + "' because it includes permission '" + permission.Code + "' that you do not hold.");
                }
            }
        }
    }
}
