using System;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class DepartmentScopeService : IDepartmentScopeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserContext _currentUser;
        private readonly IUserService _userService;
        private readonly IOrganizationScopeService _organizationScope;
        private bool _profileLoaded;
        private UserVm _profile;
        private Role _role;

        public DepartmentScopeService(
            IUnitOfWork unitOfWork,
            ICurrentUserContext currentUser,
            IUserService userService,
            IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
            _userService = userService;
            _organizationScope = organizationScope;
        }

        public bool BypassesDepartmentScope
        {
            get
            {
                if (_organizationScope != null && (_organizationScope.IsImpersonating() || _organizationScope.IsCompanyAdmin()))
                {
                    return true;
                }

                EnsureProfileLoaded();
                return _role != null && _role.IsSystemRole
                    && string.Equals(_role.Name, "Company Admin", StringComparison.OrdinalIgnoreCase);
            }
        }

        public int? ScopedDepartmentId
        {
            get
            {
                EnsureProfileLoaded();
                return BypassesDepartmentScope ? null : _profile?.DepartmentId;
            }
        }

        public IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query)
        {
            if (query == null)
            {
                return query;
            }

            if (BypassesDepartmentScope)
            {
                return query;
            }

            var departmentId = ScopedDepartmentId;
            if (!departmentId.HasValue)
            {
                return query.Where(x => false);
            }

            return query.Where(x => x.DepartmentId == departmentId.Value);
        }

        public IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query)
        {
            if (query == null)
            {
                return query;
            }

            if (BypassesDepartmentScope)
            {
                return query;
            }

            var departmentId = ScopedDepartmentId;
            if (!departmentId.HasValue)
            {
                return query.Where(x => false);
            }

            return query.Where(x => x.Id == departmentId.Value);
        }

        public int CountVisibleDepartments(bool activeOnly = true)
        {
            var query = ApplyDepartmentScope(_unitOfWork.Repository<Department>().Query());
            if (activeOnly)
            {
                query = query.Where(x => x.IsActive);
            }

            return query.Count();
        }

        public void EnsureCanAccessAsset(Asset asset)
        {
            if (asset == null || BypassesDepartmentScope)
            {
                return;
            }

            var departmentId = ScopedDepartmentId;
            if (!departmentId.HasValue)
            {
                throw new BusinessException("Your account is not assigned to a department. Contact an administrator.");
            }

            if (asset.DepartmentId != departmentId.Value)
            {
                var userId = _currentUser == null ? null : _currentUser.UserId;
                if (!string.IsNullOrWhiteSpace(userId)
                    && (ApprovalWorkflowHelper.CanAccessAssetForPendingApproval(
                            _unitOfWork,
                            _userService,
                            userId,
                            asset,
                            BypassesDepartmentScope)
                        || ApprovalWorkflowHelper.CanAccessAssetForReceiving(_unitOfWork, userId, asset)))
                {
                    return;
                }

                throw new BusinessException("This asset belongs to another department. Only administrators can access it.");
            }
        }

        public void EnsureCanAccessDepartment(Department department)
        {
            if (department == null || BypassesDepartmentScope)
            {
                return;
            }

            var departmentId = ScopedDepartmentId;
            if (!departmentId.HasValue)
            {
                throw new BusinessException("Your account is not assigned to a department. Contact an administrator.");
            }

            if (department.Id != departmentId.Value)
            {
                throw new BusinessException("This department is outside your scope. Only administrators can access it.");
            }
        }

        public void EnsureCanAccessDepartmentId(int departmentId)
        {
            if (BypassesDepartmentScope)
            {
                return;
            }

            var scopedDepartmentId = ScopedDepartmentId;
            if (!scopedDepartmentId.HasValue)
            {
                throw new BusinessException("Your account is not assigned to a department. Contact an administrator.");
            }

            if (departmentId != scopedDepartmentId.Value)
            {
                throw new BusinessException("This department is outside your scope. Only administrators can access it.");
            }
        }

        private void EnsureProfileLoaded()
        {
            if (_profileLoaded)
            {
                return;
            }

            _profileLoaded = true;
            var userId = _currentUser == null ? null : _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            _profile = _userService.GetById(userId);
            if (_profile != null && _profile.RoleId.HasValue)
            {
                _role = _unitOfWork.Repository<Role>().GetById(_profile.RoleId.Value);
            }
        }
    }
}
