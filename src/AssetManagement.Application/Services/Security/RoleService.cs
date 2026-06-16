using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Services
{
    public class RoleService : IRoleService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IAuthorizationService _authorizationService;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly ICurrentUserContext _currentUser;

        public RoleService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IAuthorizationService authorizationService,
            IOrganizationScopeService organizationScope,
            ICurrentUserContext currentUser)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _authorizationService = authorizationService;
            _organizationScope = organizationScope;
            _currentUser = currentUser;
        }

        public IEnumerable<RoleVm> GetRoles()
        {
            return _unitOfWork.Repository<Role>().GetAll()
                .OrderBy(x => x.Name)
                .Select(x => new RoleVm
                {
                    Id = x.Id,
                    Name = x.Name,
                    Description = x.Description,
                    IsActive = x.IsActive,
                    IsSystemRole = x.IsSystemRole
                })
                .ToList();
        }

        public RoleVm GetById(int id)
        {
            var role = _unitOfWork.Repository<Role>().GetById(id);
            if (role == null)
            {
                return null;
            }

            return new RoleVm
            {
                Id = role.Id,
                Name = role.Name,
                Description = role.Description,
                IsActive = role.IsActive,
                IsSystemRole = role.IsSystemRole
            };
        }

        public int Create(RoleCreateEditVm model)
        {
            if (_unitOfWork.Repository<Role>().GetAll().Any(x => x.Name == model.Name))
            {
                throw new BusinessException("Role name already exists.");
            }

            var organizationId = _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
            var role = new Role
            {
                Name = model.Name,
                Description = model.Description,
                IsActive = model.IsActive,
                IsSystemRole = model.IsSystemRole,
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow
            };

            _unitOfWork.Repository<Role>().Add(role);
            _unitOfWork.SaveChanges();
            SetPermissions(role.Id, model.SelectedPermissionIds);
            _auditWriter.Write("Roles.Create", nameof(Role), role.Id.ToString(), null, role.Name);
            return role.Id;
        }

        public void Update(RoleCreateEditVm model)
        {
            var role = _unitOfWork.Repository<Role>().GetById(model.Id);
            if (role == null)
            {
                throw new BusinessException("Role not found.");
            }

            if (role.IsSystemRole && !role.IsActive && model.IsActive)
            {
                throw new BusinessException("System roles cannot be re-activated this way.");
            }

            role.Name = model.Name;
            role.Description = model.Description;
            role.IsActive = model.IsActive;
            role.UpdatedAt = DateTime.UtcNow;

            _unitOfWork.Repository<Role>().Update(role);
            _unitOfWork.SaveChanges();
            SetPermissions(role.Id, model.SelectedPermissionIds);
            _auditWriter.Write("Roles.Edit", nameof(Role), role.Id.ToString(), null, role.Name);
        }

        public void SetPermissions(int roleId, IEnumerable<int> permissionIds)
        {
            var role = _unitOfWork.Repository<Role>().GetById(roleId);
            if (role == null)
            {
                throw new BusinessException("Role not found.");
            }

            var requested = permissionIds?.Distinct().ToList() ?? new List<int>();
            EnsurePermissionCeiling(requested);

            var organizationId = role.OrganizationId
                ?? (_organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId());

            var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();
            var current = rolePermissionRepo.Find(x => x.RoleId == roleId).ToList();
            foreach (var item in current)
            {
                rolePermissionRepo.Remove(item);
            }

            foreach (var permissionId in requested)
            {
                rolePermissionRepo.Add(new RolePermission
                {
                    RoleId = roleId,
                    PermissionId = permissionId,
                    OrganizationId = organizationId
                });
            }

            _unitOfWork.SaveChanges();
            _auditWriter.Write("Permissions.Assign", nameof(RolePermission), roleId.ToString(), null, string.Join(",", requested));
        }

        private void EnsurePermissionCeiling(IEnumerable<int> permissionIds)
        {
            if (_organizationScope != null && _organizationScope.IsCompanyAdmin())
            {
                return;
            }

            var actorId = _currentUser == null ? null : _currentUser.UserId;
            if (string.IsNullOrWhiteSpace(actorId))
            {
                throw new BusinessException("You must be signed in to assign permissions.");
            }

            var ids = permissionIds == null ? new List<int>() : permissionIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return;
            }

            var permissions = _unitOfWork.Repository<Permission>()
                .Find(x => ids.Contains(x.Id))
                .ToList();

            foreach (var permission in permissions)
            {
                if (!_authorizationService.HasPermission(actorId, permission.Code))
                {
                    throw new BusinessException(
                        "You cannot grant permission '" + permission.Code + "' because you do not hold it.");
                }
            }
        }
    }
}
