using System;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class AssetAssignmentVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Asset is required.")]
        public int AssetId { get; set; }

        [Display(Name = "To Department")]
        public int? ToDepartmentId { get; set; }

        [Display(Name = "To User")]
        [StringLength(128)]
        public string ToUserId { get; set; }

        public string ToUserName { get; set; }

        public string ToDepartmentName { get; set; }

        /// <summary>
        /// Move = update asset department to selected/teacher department;
        /// Keep = leave asset in its current department (ownership only).
        /// Required when assigning a person whose department differs from the asset's.
        /// </summary>
        [Display(Name = "Department disposition")]
        [StringLength(10)]
        public string DepartmentDisposition { get; set; }

        /// <summary>
        /// When assigning into a Class/Room with optional PIC set and no To user chosen:
        /// Pic = assign under ClassTeacher/RoomCustodian; Pool = leave department pool.
        /// </summary>
        [Display(Name = "PIC placement")]
        [StringLength(10)]
        public string PicPlacementChoice { get; set; }

        [Display(Name = "Assignment Type")]
        [StringLength(40)]
        public string AssignmentType { get; set; }

        [Required]
        [Display(Name = "Assigned Date")]
        public DateTime AssignedDate { get; set; }

        [Display(Name = "Expected Return Date")]
        public DateTime? ExpectedReturnDate { get; set; }

        [Display(Name = "Condition Before Handover")]
        [StringLength(200)]
        public string ConditionBeforeHandover { get; set; }

        [Display(Name = "Accessories Handed Over")]
        [StringLength(300)]
        public string AccessoriesHandedOver { get; set; }

        [Display(Name = "Handover Notes")]
        [StringLength(1000)]
        public string HandoverNotes { get; set; }

        [StringLength(128)]
        public string HandedOverById { get; set; }

        [StringLength(128)]
        public string ReceivedById { get; set; }
    }

    public class AssetTransferVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Asset is required.")]
        public int AssetId { get; set; }

        [Display(Name = "From User")]
        [StringLength(128)]
        public string FromUserId { get; set; }

        [Display(Name = "To User")]
        [StringLength(128)]
        public string ToUserId { get; set; }

        [Display(Name = "From Department")]
        public int? FromDepartmentId { get; set; }

        [Display(Name = "To Department")]
        public int? ToDepartmentId { get; set; }

        public string ToDepartmentName { get; set; }

        /// <summary>
        /// Move = update asset department to selected/teacher department;
        /// Keep = leave asset in its current department (ownership only).
        /// Required when transferring to a person whose department differs from the asset's.
        /// </summary>
        [Display(Name = "Department disposition")]
        [StringLength(10)]
        public string DepartmentDisposition { get; set; }

        [StringLength(1000)]
        public string Reason { get; set; }

        [Display(Name = "Condition Before")]
        [StringLength(200)]
        public string ConditionBefore { get; set; }

        [Display(Name = "Condition After")]
        [StringLength(200)]
        public string ConditionAfter { get; set; }

        [Display(Name = "Missing Accessories")]
        public bool MissingAccessories { get; set; }

        [Display(Name = "Damage Notes")]
        [StringLength(1000)]
        public string DamageNotes { get; set; }
    }

    public class AssetReturnVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Asset is required.")]
        public int AssetId { get; set; }

        [Display(Name = "Returned By")]
        [StringLength(128)]
        public string ReturnedById { get; set; }

        [Display(Name = "Received By")]
        [StringLength(128)]
        public string ReceivedById { get; set; }

        [Required]
        [Display(Name = "Return Date")]
        public DateTime ReturnDate { get; set; }

        [Required(ErrorMessage = "Return condition is required.")]
        [Display(Name = "Return Condition")]
        [StringLength(200)]
        public string ReturnCondition { get; set; }

        [Display(Name = "Missing Accessories")]
        public bool MissingAccessories { get; set; }

        [Display(Name = "Damage Notes")]
        [StringLength(1000)]
        public string DamageNotes { get; set; }

        [StringLength(1000)]
        public string Notes { get; set; }
    }

    public class PurchaseRecordVm
    {
        public int Id { get; set; }

        /// <summary>Optional link to an approved purchase requisition.</summary>
        public int? PurchaseRequestId { get; set; }

        public string PurchaseRequestNumber { get; set; }

        public string PurchaseOrderNumber { get; set; }

        public int SupplierId { get; set; }

        public string SupplierName { get; set; }

        public string InvoiceNumber { get; set; }

        public DateTime PurchaseDate { get; set; }

        public int Quantity { get; set; }

        public decimal UnitCost { get; set; }

        public decimal TotalCost { get; set; }

        public string Currency { get; set; }
    }

    public class AssetMaintenanceVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Asset is required.")]
        public int AssetId { get; set; }

        [Required]
        [StringLength(1000)]
        public string ReportedIssue { get; set; }

        [Required]
        [StringLength(40)]
        public string MaintenanceType { get; set; }
    }

    public class AssetIncidentVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Asset is required.")]
        public int AssetId { get; set; }

        [Required]
        [StringLength(40)]
        public string IncidentType { get; set; }

        [Required(ErrorMessage = "Severity is required.")]
        [StringLength(40)]
        public string Severity { get; set; }

        [Required]
        public DateTime IncidentDate { get; set; }

        [Required]
        [StringLength(2000)]
        public string Description { get; set; }
    }

    public class InsuranceClaimVm
    {
        [Range(1, int.MaxValue, ErrorMessage = "Asset is required.")]
        public int AssetId { get; set; }

        public int? IncidentId { get; set; }

        [Required]
        public DateTime ClaimDate { get; set; }

        [Required]
        [StringLength(80)]
        public string ClaimType { get; set; }

        [Required]
        [StringLength(160)]
        public string Insurer { get; set; }
    }
}
