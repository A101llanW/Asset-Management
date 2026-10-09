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
        private readonly IUserLifecycleStore _lifecycleStore;

        /// <summary>DI constructor (rel1009g): adds the user lifecycle store for deactivate / delete.</summary>
        public UserService(
            IUnitOfWork unitOfWork,
            IAuthorizationService authorizationService,
            IOrganizationScopeService organizationScope,
            ICurrentUserContext currentUser,
            IAuditWriter auditWriter,
            IUserAccountQueryRepository userAccountQueryRepository,
            IReferenceDataCache referenceDataCache,
            IUserLifecycleStore lifecycleStore)
            : this(unitOfWork, authorizationService, organizationScope, currentUser, auditWriter, userAccountQueryRepository, referenceDataCache)
        {
            _lifecycleStore = lifecycleStore;
        }

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
            // rel1009e: a department may have any number of Department Heads (one-head rule removed).

            var previousRoleId = user.RoleId;
            user.RoleId = roleId;
            user.UpdatedAt = System.DateTime.UtcNow;
            user.AccessToken = Application.Security.SecurePasswordGenerator.GenerateAccessToken();
            _unitOfWork.Writer<ApplicationUser>().Update(user);
            _unitOfWork.SaveChanges();
            InvalidateUserCache(user.OrganizationId);
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


            var previousDepartmentId = user.DepartmentId;
            user.DepartmentId = departmentId;
            user.UpdatedAt = System.DateTime.UtcNow;
            _unitOfWork.Writer<ApplicationUser>().Update(user);
            _unitOfWork.SaveChanges();
            InvalidateUserCache(user.OrganizationId);
            _auditWriter.Write(
                "Users.AssignDepartment",
                nameof(ApplicationUser),
                userId,
                previousDepartmentId.HasValue ? previousDepartmentId.Value.ToString() : null,
                departmentId.HasValue ? departmentId.Value.ToString() : null);
        }

        public void SetActive(string userId, bool isActive)
        {
            var user = LoadTenantUser(userId);
            var organizationId = user.OrganizationId.Value;
            if (user.IsActive == isActive)
            {
                return;
            }

            EnsureLifecycleAllowed(user, isActive ? UserLifecycleAction.Reactivate : UserLifecycleAction.Deactivate);
            user.IsActive = isActive;
            user.UpdatedAt = System.DateTime.UtcNow;
            // Rotating the access token ends every existing session (ValidateAccessToken also rejects inactive users).
            user.AccessToken = Application.Security.SecurePasswordGenerator.GenerateAccessToken();
            _unitOfWork.Writer<ApplicationUser>().Update(user);
            _unitOfWork.SaveChanges();
            InvalidateUserCache(organizationId);
            _auditWriter.Write(
                isActive ? "Users.Reactivate" : "Users.Deactivate",
                nameof(ApplicationUser),
                userId,
                isActive ? "Inactive" : "Active",
                isActive ? "Active" : "Inactive");
        }

        public IList<KeyValuePair<string, int>> GetDeleteBlockers(string userId)
        {
            var user = LoadTenantUser(userId);
            return RequireLifecycleStore().GetDeleteBlockers(user.Id, user.OrganizationId.Value);
        }

        public void Delete(string userId)
        {
            var user = LoadTenantUser(userId);
            var organizationId = user.OrganizationId.Value;
            EnsureLifecycleAllowed(user, UserLifecycleAction.Delete);
            var store = RequireLifecycleStore();
            var blocked = UserLifecycleRules.BuildBlockedMessage(store.GetDeleteBlockers(user.Id, organizationId));
            if (blocked != null)
            {
                throw new BusinessException(blocked);
            }

            var summary = "Email=" + user.Email + ";Name=" + ((user.FirstName ?? string.Empty) + " " + (user.LastName ?? string.Empty)).Trim()
                + ";RoleId=" + (user.RoleId.HasValue ? user.RoleId.Value.ToString() : string.Empty);
            store.HardDelete(user.Id, organizationId);
            InvalidateUserCache(organizationId);
            _auditWriter.Write("Users.Delete", nameof(ApplicationUser), userId, summary, null);
        }

        private ApplicationUser LoadTenantUser(string userId)
        {
            var organizationId = _organizationScope.GetCurrentOrganizationId();
            var user = string.IsNullOrWhiteSpace(userId) ? null : _unitOfWork.Writer<ApplicationUser>().GetById(userId);
            if (user == null || !organizationId.HasValue || !user.OrganizationId.HasValue || user.OrganizationId.Value != organizationId.Value)
            {
                throw new BusinessException("User not found.");
            }

            return user;
        }

        private void EnsureLifecycleAllowed(ApplicationUser user, UserLifecycleAction action)
        {
            var store = RequireLifecycleStore();
            var organizationId = user.OrganizationId.Value;
            var targetIsCompanyAdmin = store.IsCompanyAdmin(user.Id, organizationId);
            var error = UserLifecycleRules.Evaluate(
                action,
                _currentUser == null ? null : _currentUser.UserId,
                user.Id,
                user.IsActive,
                targetIsCompanyAdmin,
                targetIsCompanyAdmin ? store.CountOtherActiveCompanyAdmins(organizationId, user.Id) : 1);
            if (error != null)
            {
                throw new BusinessException(error);
            }

            // Escalation guard: only Company Admins act on a Company Admin; others must hold every permission of the target's role.
            if (targetIsCompanyAdmin && !_organizationScope.IsCompanyAdmin() && !_organizationScope.IsActualPlatformAdmin())
            {
                throw new BusinessException("Only Company Administrators can change or delete a Company Admin account.");
            }

            if (user.RoleId.HasValue)
            {
                EnsurePermissionCeiling(user.RoleId.Value);
            }
        }

        private IUserLifecycleStore RequireLifecycleStore()
        {
            if (_lifecycleStore == null)
            {
                throw new BusinessException("User deactivation and deletion are not available in this context.");
            }

            return _lifecycleStore;
        }

        private void InvalidateUserCache(int? organizationId)
        {
            var orgId = organizationId ?? _organizationScope.GetCurrentOrganizationId();
            if (_referenceDataCache != null && orgId.HasValue)
            {
                _referenceDataCache.InvalidateUsers(orgId.Value);
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
