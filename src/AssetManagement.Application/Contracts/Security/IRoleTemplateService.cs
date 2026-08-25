using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IRoleTemplateService
    {
        IEnumerable<RoleTemplateVm> GetTemplates();

        RoleTemplateVm GetById(int id);

        IList<int> GetPermissionIds(int templateId);

        int CreateFromRole(int roleId, string templateName);
    }
}
