using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class RoleVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public bool IsSystemRole { get; set; }

        public bool IsActive { get; set; }
    }

    public class RoleCreateEditVm
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsActive { get; set; } = true;

        public bool IsSystemRole { get; set; }

        public List<int> SelectedPermissionIds { get; set; } = new List<int>();

        public IEnumerable<PermissionGroupVm> PermissionGroups { get; set; } = new List<PermissionGroupVm>();

        public IEnumerable<RoleTemplateVm> RoleTemplates { get; set; } = new List<RoleTemplateVm>();
    }

    public class RoleTemplateVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public int PermissionCount { get; set; }

        public int? SourceRoleId { get; set; }
    }

    public class RoleTemplateSaveVm
    {
        public int RoleId { get; set; }

        [Required]
        [StringLength(120)]
        public string TemplateName { get; set; }
    }

    public class PermissionVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Code { get; set; }

        public string Module { get; set; }

        public string Description { get; set; }
    }

    public class PermissionGroupVm
    {
        public string Module { get; set; }

        public IEnumerable<PermissionVm> Permissions { get; set; } = new List<PermissionVm>();
    }

    public class UserVm
    {
        public string Id { get; set; }

        public string EmployeeNumber { get; set; }

        public string FirstName { get; set; }

        public string LastName { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public int? DepartmentId { get; set; }

        public string DepartmentName { get; set; }

        public string PositionTitle { get; set; }

        public bool IsActive { get; set; }

        public int? RoleId { get; set; }

        public string RoleName { get; set; }
    }
}
