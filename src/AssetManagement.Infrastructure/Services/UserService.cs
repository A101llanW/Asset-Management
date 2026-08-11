using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.Security;
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
        private readonly IReferenceDataCache _referenceDataCache;

        public UserService(
            IUnitOfWork unitOfWork,
            IAuthorizationService authorizationService,
            IOrganizationScopeService organizationScope,
            ICurrentUserContext currentUser,
            IAuditWriter auditWriter,
            IUserAccountQueryRepository userAccountQueryRepository,
            IReferenceDataCache referenceDataCache)
        {
            _unitOfWork = unitOfWork;
            _authorizationService = authorizationService;
            _organizationScope = organizationScope;
            _currentUser = currentUser;
            _auditWriter = auditWriter;
            _userAccountQueryRepository = userAccountQueryRepository;
            _referenceDataCache = referenceDataCache;
        }

        public IEnumerable<UserVm> GetAll()
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new List<UserVm>();
            }

            var users = _userAccountQueryRepository.GetUsersForOrganization(
                organizationId.Value,
                null,
                true);
            ApplyOrganizationRoleNames(users, organizationId.Value);
            return users;
        }

        public PagedListVm<UserVm> GetListPage(
            UserListFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return new PagedListVm<UserVm>();
            }

            var result = _userAccountQueryRepository.GetUserListPage(
                organizationId.Value,
                null,
                true,
                filter,
                sort,
                direction,
                page,
                pageSize);
            ApplyOrganizationRoleNames(result.Items, organizationId.Value);
            return result;
        }

        public UserVm GetById(string id)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                return null;
            }

            var user = _userAccountQueryRepository.GetUserById(id, organizationId.Value);
            if (user == null)
            {
                return null;
            }

            ApplyOrganizationRoleNames(new[] { user }, organizationId.Value);
            return user;
        }

        private void ApplyOrganizationRoleNames(IEnumerable<UserVm> users, int organizationId)
        {
            if (_referenceDataCache == null || users == null)
            {
                return;
            }

            UserRoleNameResolver.ApplyOrganizationRoleNames(users, _referenceDataCache.GetRoles(organizationId));
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

            if (user.OrganizationId.HasValue
                && role.OrganizationId.HasValue
                && role.OrganizationId.Value != user.OrganizationId.Value)
            {
                throw new BusinessException("That role does not belong to this organization.");
            }

            if (role.IsSystemRole
                && !_organizationScope.IsCompanyAdmin()
                && !_organizationScope.IsActualPlatformAdmin())
            {
                throw new BusinessException("Only Company Administrators can assign system roles.");
            }

            EnsurePermissionCeiling(roleId);
            EnsureDepartmentHeadAssignment(user, user.DepartmentId, roleId);

            var previousRoleId = user.RoleId;
            user.RoleId = roleId;
            user.UpdatedAt = System.DateTime.UtcNow;
            user.AccessToken = Application.Security.SecurePasswordGenerator.GenerateAccessToken();
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
                throw new BusinessException("User not found.");
            }

            if (departmentId.HasValue)
            {
                var department = _unitOfWork.Writer<Department>().GetById(departmentId.Value);
                if (department == null)
                {
                    throw new BusinessException("That department no longer exists.");
                }
            }

            EnsureDepartmentHeadAssignment(user, departmentId, user.RoleId);

            var previousDepartmentId = user.DepartmentId;
            user.DepartmentId = departmentId;
            user.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Writer<ApplicationUser>().Update(user);
            _unitOfWork.SaveChanges();
            _auditWriter.Write(
                "Users.AssignDepartment",
                nameof(ApplicationUser),
                userId,
                previousDepartmentId.HasValue ? previousDepartmentId.Value.ToString() : null,
                departmentId.HasValue ? departmentId.Value.ToString() : null);
        }

        private void EnsureDepartmentHeadAssignment(ApplicationUser user, int? departmentId, int? roleId)
        {
            if (user == null || !roleId.HasValue)
            {
                return;
            }

            var role = _unitOfWork.Writer<Role>().GetById(roleId.Value);
            if (!DepartmentHeadAssignmentRules.IsDepartmentHeadRole(role))
            {
                return;
            }

            if (!departmentId.HasValue)
            {
                throw new BusinessException("Assign a department before making this user a Department Head.");
            }

            var organizationId = user.OrganizationId;
            var peers = _unitOfWork.Repository<ApplicationUser>()
                .Find(u => u.Id != user.Id
                    && u.IsActive
                    && u.OrganizationId == organizationId
                    && u.DepartmentId == departmentId
                    && u.RoleId.HasValue)
                .ToList();

            foreach (var peer in peers)
            {
                var peerRole = _unitOfWork.Writer<Role>().GetById(peer.RoleId.Value);
                if (!DepartmentHeadAssignmentRules.IsDepartmentHeadRole(peerRole))
                {
                    continue;
                }

                var department = _unitOfWork.Writer<Department>().GetById(departmentId.Value);
                var departmentName = department == null || string.IsNullOrWhiteSpace(department.Name)
                    ? "this department"
                    : department.Name;
                var peerName = ((peer.FirstName ?? string.Empty) + " " + (peer.LastName ?? string.Empty)).Trim();
                if (string.IsNullOrWhiteSpace(peerName))
                {
                    peerName = peer.Email;
                }

                throw new BusinessException(
                    "Cannot assign Department Head: "
                    + departmentName
                    + " already has a Department Head ("
                    + peerName
                    + "). Each department may have only one head, and a head may lead only one department.");
            }
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
