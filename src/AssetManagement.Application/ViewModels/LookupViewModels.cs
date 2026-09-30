using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class DepartmentVm
    {
        public int Id { get; set; }

        [Required]
        [StringLength(120)]
        public string Name { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public int? ParentDepartmentId { get; set; }

        public string ParentDepartmentName { get; set; }

        public DepartmentKind DepartmentKind { get; set; }

        public bool IsRequisitionTarget { get; set; }

        public bool IsActive { get; set; }

        public RequisitionFlowMode RequisitionFlowMode { get; set; }

        public string CustomStageRoleIds { get; set; }

        public string CustomStageUserIds { get; set; }

        public string EffectiveRequisitionFlowSummary { get; set; }

        public IList<ApprovalStageSettingsVm> CustomStages { get; set; } = new List<ApprovalStageSettingsVm>();

        public IList<DepartmentVm> Children { get; set; } = new List<DepartmentVm>();
    }

    public class DepartmentCreateVm
    {
        public string SetupMode { get; set; }

        [StringLength(120)]
        public string Name { get; set; }

        [StringLength(20)]
        public string Code { get; set; }

        [StringLength(500)]
        public string Description { get; set; }

        public bool IsRequisitionTarget { get; set; } = true;

        public int? ParentDepartmentId { get; set; }

        public int? GradeNumber { get; set; }

        public string SelectedStreams { get; set; }

        public int BulkGradeFrom { get; set; } = 1;

        public int BulkGradeTo { get; set; } = 12;

        public string BulkStreams { get; set; } = "A,B,C,D";
    }


    public class ResolvedRequisitionFlowStageVm
    {
        public int StageNumber { get; set; }

        public int? RoleId { get; set; }

        public string RoleName { get; set; }

        public string UserId { get; set; }

        public string UserName { get; set; }

        public string DisplayLabel { get; set; }
    }

    public class ResolvedRequisitionFlowPreviewVm
    {
        public bool HasApprovers { get; set; }

        public string SourceKind { get; set; }

        public string SourceLabel { get; set; }

        public string Summary { get; set; }

        public IList<ResolvedRequisitionFlowStageVm> Stages { get; set; } = new List<ResolvedRequisitionFlowStageVm>();
    }

    public class DepartmentTreeSectionVm
    {
        public string Title { get; set; }

        public IList<DepartmentVm> Items { get; set; } = new List<DepartmentVm>();
    }

    public class SupplierVm
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string SupplierName { get; set; }

        [StringLength(120)]
        public string ContactPerson { get; set; }

        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Invalid email address.")]
        public string Email { get; set; }

        [StringLength(50)]
        public string Phone { get; set; }

        [StringLength(500)]
        public string Address { get; set; }

        [StringLength(100)]
        public string RegistrationNumber { get; set; }

        [StringLength(50)]
        public string TaxId { get; set; }

        [StringLength(100)]
        public string PaymentTerms { get; set; }

        public int? DefaultLeadTimeDays { get; set; }

        [StringLength(300)]
        public string Website { get; set; }

        public bool IsPreferred { get; set; }

        [StringLength(100)]
        public string Country { get; set; }

        public string PaymentInstructions { get; set; }

        public string Notes { get; set; }

        public bool IsActive { get; set; }

        public int CatalogItemCount { get; set; }

        public decimal? CatalogMinPrice { get; set; }

        public decimal? CatalogMaxPrice { get; set; }
    }

    public class CategoryLookupVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public bool IsActive { get; set; }
    }

    public class AssetTypeLookupVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int AssetCategoryId { get; set; }

        public bool IsActive { get; set; }
    }
}
