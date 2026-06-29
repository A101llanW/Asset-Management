using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class PurchaseRequestCreateVm
    {
        [Required(ErrorMessage = "Department is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Department is required.")]
        public int DepartmentId { get; set; }

        public bool RequestForSelf { get; set; } = true;

        public string OrderByUserId { get; set; }

        [Required(ErrorMessage = "Item description is required.")]
        [StringLength(2000)]
        public string ItemDescription { get; set; }

        [Required]
        [StringLength(2000)]
        public string Justification { get; set; }

        public int? QuantityInStock { get; set; }

        public DateTime? RequiredDate { get; set; }

        [Required]
        public decimal? EstimatedUnitCost { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; }

        [StringLength(2000)]
        public string Notes { get; set; }

        /// <summary>Optional existing asset to tag for easier assignment after purchase.</summary>
        public int? TargetAssetId { get; set; }
    }

    public class PurchaseRequestListItemVm
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string DepartmentName { get; set; }

        public string RequestedById { get; set; }

        public string ApprovalStatus { get; set; }

        public DateTime CreatedAt { get; set; }

        public decimal EstimatedUnitCost { get; set; }

        public int Quantity { get; set; }

        public string Currency { get; set; }

        public string ItemDescription { get; set; }
    }

    public class PurchaseRequestDetailVm
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string RequestedById { get; set; }

        public string RequestedByName { get; set; }

        public string OrderByUserId { get; set; }

        public string OrderByUserName { get; set; }

        public string ApprovedById { get; set; }

        public string ApprovalStatus { get; set; }

        public int DepartmentId { get; set; }

        public string DepartmentName { get; set; }

        public string ItemDescription { get; set; }

        public string Justification { get; set; }

        public int? QuantityInStock { get; set; }

        public DateTime? RequiredDate { get; set; }

        public decimal EstimatedUnitCost { get; set; }

        public int Quantity { get; set; }

        public string Currency { get; set; }

        public string Notes { get; set; }

        public string AttachmentFileName { get; set; }

        public string AttachmentContentType { get; set; }

        public bool HasAttachment { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public int CurrentApprovalStage { get; set; }

        public int? CurrentStageRoleId { get; set; }

        public string CurrentStageRoleName { get; set; }

        public string CurrentStageUserId { get; set; }

        public string CurrentStageUserName { get; set; }

        public bool CanCurrentUserApprove { get; set; }

        public bool IsPending { get; set; }

        public bool IsApproved { get; set; }

        public bool HasPurchaseRecord { get; set; }

        public int? LinkedPurchaseRecordId { get; set; }

        public int? TargetAssetId { get; set; }

        public string TargetAssetTag { get; set; }

        public string TargetAssetName { get; set; }

        public IEnumerable<ApprovalDecisionHistoryVm> ApprovalHistory { get; set; } = new List<ApprovalDecisionHistoryVm>();
    }

    public class PurchaseRequestApprovalVm
    {
        [Required]
        public int PurchaseRequestId { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }

    public class PurchaseRequestAttachmentInfo
    {
        public string FileName { get; set; }

        public string FilePath { get; set; }

        public string ContentType { get; set; }

        public long FileSizeBytes { get; set; }
    }
}
