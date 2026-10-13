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

        public RequisitionFlowMode RequisitionFlowMode { get; set; }

        /// <summary>Inherit mode only: null = nearest ancestor (default); SubDepartment / Department = that ancestor's flow.</summary>
        public RequisitionInheritSource? RequisitionInheritFrom { get; set; }

        /// <summary>Edit: new parents for active children that the Kind/Parent change would make invalid (same save).</summary>
        public IList<DepartmentChildMoveVm> ChildMoves { get; set; } = new List<DepartmentChildMoveVm>();

        /// <summary>Custom on X / Inherited from Y / Organization Approval Matrix</summary>
        public string EffectiveRequisitionFlowSource { get; set; }

        public string EffectiveRequisitionFlowSummary { get; set; }

        /// <summary>Custom flow only: role that raises the PO after final approval (null = not configured).</summary>
        public int? PurchaseOrderRaiserRoleId { get; set; }

        /// <summary>Custom flow only: optional person in <see cref="PurchaseOrderRaiserRoleId"/> (null = any role holder).</summary>
        [StringLength(128)]
        public string PurchaseOrderRaiserUserId { get; set; }

        /// <summary>Display: effective PO raiser, e.g. "PO: Procurement Officer" / "PO: inherited from Sub: Science".</summary>
        public string EffectivePurchaseOrderRaiserSummary { get; set; }

        public bool IsActive { get; set; }

        public bool UseCustomRequisitionApproval { get; set; }

        /// <summary>Optional Class teacher (user id). Clearable; never required.</summary>
        [StringLength(128)]
        [Display(Name = "Classroom teacher")]
        public string ClassTeacherUserId { get; set; }

        public string ClassTeacherName { get; set; }

        /// <summary>Optional Room custodian (user id). Clearable; never required.</summary>
        [StringLength(128)]
        [Display(Name = "Room custodian")]
        public string RoomCustodianUserId { get; set; }

        public string RoomCustodianName { get; set; }

        public IList<ApprovalProcessSettingsVm> RequisitionApprovalProcesses { get; set; } = new List<ApprovalProcessSettingsVm>();

        public IList<DepartmentVm> Children { get; set; } = new List<DepartmentVm>();
    }

    public class DepartmentChildMoveVm
    {
        public int ChildDepartmentId { get; set; }

        /// <summary>Null = not chosen; 0 = no parent (independent); otherwise the new parent department id.</summary>
        public int? NewParentDepartmentId { get; set; }
    }

    public class DepartmentParentOptionVm
    {
        /// <summary>0 = no parent (independent).</summary>
        public int Id { get; set; }

        public string Label { get; set; }
    }

    public class DepartmentKindChangeImpactItemVm
    {
        public int ChildId { get; set; }

        public string ChildName { get; set; }

        public string ChildCode { get; set; }

        public string ChildKindLabel { get; set; }

        public string Reason { get; set; }

        public IList<DepartmentParentOptionVm> Candidates { get; set; } = new List<DepartmentParentOptionVm>();
    }

    public class DepartmentKindChangeImpactVm
    {
        public int DepartmentId { get; set; }

        public string NewKindLabel { get; set; }

        public IList<DepartmentKindChangeImpactItemVm> Items { get; set; } = new List<DepartmentKindChangeImpactItemVm>();
    }

    public class RequisitionInheritOptionVm
    {
        public RequisitionInheritSource Source { get; set; }

        public int AncestorId { get; set; }

        public string AncestorName { get; set; }

        /// <summary>e.g. "Sub-department: Office Services" / "Department: Facilities".</summary>
        public string Label { get; set; }
    }

    /// <summary>
    /// What a unit would resolve to if it inherits with a given "Inherit flow from" choice (Choice "" = nearest parent,
    /// else the RequisitionInheritSource name used as the option value). Used by the live summary on Departments Edit.
    /// </summary>
    public class RequisitionInheritPreviewVm
    {
        public string Choice { get; set; }

        public string FlowSummary { get; set; }

        public string PurchaseOrderRaiserSummary { get; set; }
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

        /// <summary>
        /// When set, POST must use the same parent department (deep-link create flows).
        /// </summary>
        public int? LockedParentDepartmentId { get; set; }

        public int? GradeNumber { get; set; }

        public string SelectedStreams { get; set; }

        public int BulkGradeFrom { get; set; } = 1;

        public int BulkGradeTo { get; set; } = 6;

        public string BulkStreams { get; set; } = "A,B,C,D";

        /// <summary>Optional Room custodian when creating a Room. Never required.</summary>
        [StringLength(128)]
        public string RoomCustodianUserId { get; set; }

        /// <summary>Optional Class teacher when creating a Class (future single-class create). Never required.</summary>
        [StringLength(128)]
        public string ClassTeacherUserId { get; set; }
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
