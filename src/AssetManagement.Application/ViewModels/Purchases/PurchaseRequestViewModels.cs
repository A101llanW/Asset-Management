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

        [Required]
        [StringLength(2000)]
        public string Justification { get; set; }

        [Required]
        public decimal? EstimatedUnitCost { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; }

        [Required]
        [StringLength(10)]
        public string Currency { get; set; }

        [StringLength(2000)]
        public string Notes { get; set; }
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
    }

    public class PurchaseRequestDetailVm
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string RequestedById { get; set; }

        public string ApprovedById { get; set; }

        public string ApprovalStatus { get; set; }

        public int DepartmentId { get; set; }

        public string DepartmentName { get; set; }

        public string Justification { get; set; }

        public decimal EstimatedUnitCost { get; set; }

        public int Quantity { get; set; }

        public string Currency { get; set; }

        public string Notes { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public int CurrentApprovalStage { get; set; }

        public int? CurrentStageRoleId { get; set; }

        public string CurrentStageRoleName { get; set; }

        public bool CanCurrentUserApprove { get; set; }

        public bool IsPending { get; set; }

        public bool IsApproved { get; set; }

        public bool HasPurchaseRecord { get; set; }

        public int? LinkedPurchaseRecordId { get; set; }

        public IEnumerable<ApprovalDecisionHistoryVm> ApprovalHistory { get; set; } = new List<ApprovalDecisionHistoryVm>();
    }

    public class PurchaseRequestApprovalVm
    {
        [Required]
        public int PurchaseRequestId { get; set; }

        [StringLength(500)]
        public string Notes { get; set; }
    }
}
