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
    public class RoleTemplateService : IRoleTemplateService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IOrganizationScopeService _organizationScope;

        public RoleTemplateService(
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            IOrganizationScopeService organizationScope)
        {
            _unitOfWork = unitOfWork;
            _auditWriter = auditWriter;
            _organizationScope = organizationScope;
        }

        public IEnumerable<RoleTemplateVm> GetTemplates()
        {
            var organizationId = ResolveOrganizationId();
            return _unitOfWork.Repository<RoleTemplate>().GetAll()
                .Where(x => x.IsActive && (!organizationId.HasValue || x.OrganizationId == organizationId.Value))
                .OrderBy(x => x.Name)
                .Select(ToVm)
                .ToList();
        }

        public RoleTemplateVm GetById(int id)
        {
            var template = GetScopedTemplate(id);
            return template == null ? null : ToVm(template);
        }

        public IList<int> GetPermissionIds(int templateId)
        {
            var template = GetScopedTemplate(templateId);
            if (template == null)
            {
                throw new BusinessException("Role template not found.");
            }

            return ApprovalWorkflowSettingsHelper.ParseStageRoleIds(template.PermissionIds);
        }

        public int CreateFromRole(int roleId, string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
            {
                throw new BusinessException("Template name is required.");
            }

            var role = _unitOfWork.Repository<Role>().GetById(roleId);
            if (role == null)
            {
                throw new BusinessException("Role not found.");
            }

            var organizationId = role.OrganizationId ?? ResolveOrganizationId();
            EnsureOrganizationScope(organizationId);

            var normalizedName = templateName.Trim();
            if (_unitOfWork.Repository<RoleTemplate>().GetAll().Any(x =>
                x.IsActive
                && x.OrganizationId == organizationId
                && string.Equals(x.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BusinessException("A role template with that name already exists.");
            }

            var permissionIds = _unitOfWork.Repository<RolePermission>()
                .Find(x => x.RoleId == roleId)
                .Select(x => x.PermissionId)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            var template = new RoleTemplate
            {
                Name = normalizedName,
                Description = role.Description,
                PermissionIds = ApprovalWorkflowSettingsHelper.SerializeStageRoleIds(permissionIds.Select(x => (int?)x)),
                SourceRoleId = roleId,
                OrganizationId = organizationId,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _unitOfWork.Repository<RoleTemplate>().Add(template);
            _unitOfWork.SaveChanges();
            _auditWriter.Write("Roles.Template.Create", nameof(RoleTemplate), template.Id.ToString(), roleId.ToString(), template.Name);
            return template.Id;
        }

        private RoleTemplate GetScopedTemplate(int id)
        {
            var template = _unitOfWork.Repository<RoleTemplate>().GetById(id);
            if (template == null || !template.IsActive)
            {
                return null;
            }

            var organizationId = ResolveOrganizationId();
            if (organizationId.HasValue && template.OrganizationId.HasValue && template.OrganizationId.Value != organizationId.Value)
            {
                return null;
            }

            return template;
        }

        private void EnsureOrganizationScope(int? organizationId)
        {
            var currentOrganizationId = ResolveOrganizationId();
            if (currentOrganizationId.HasValue
                && organizationId.HasValue
                && currentOrganizationId.Value != organizationId.Value)
            {
                throw new BusinessException("You do not have access to this role.");
            }
        }

        private int? ResolveOrganizationId()
        {
            return _organizationScope == null ? null : _organizationScope.GetCurrentOrganizationId();
        }

        private static RoleTemplateVm ToVm(RoleTemplate template)
        {
            var permissionIds = ApprovalWorkflowSettingsHelper.ParseStageRoleIds(template.PermissionIds);
            return new RoleTemplateVm
            {
                Id = template.Id,
                Name = template.Name,
                Description = template.Description,
                PermissionCount = permissionIds.Count,
                SourceRoleId = template.SourceRoleId
            };
        }
    }
}
