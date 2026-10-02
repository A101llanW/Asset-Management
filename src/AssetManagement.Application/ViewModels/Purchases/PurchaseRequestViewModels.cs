using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class PurchaseRequestLineCreateVm
    {
        [StringLength(2000)]
        public string Description { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;
    }

    public class PurchaseRequestLineVm
    {
        public int LineNumber { get; set; }

        public string Description { get; set; }

        public int Quantity { get; set; }
    }

    public class PurchaseRequestCreateVm : IValidatableObject
    {
        [Required(ErrorMessage = "Department is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Department is required.")]
        public int DepartmentId { get; set; }

        public bool RequestForSelf { get; set; } = true;

        public string OrderByUserId { get; set; }

        /// <summary>Legacy single-line field; populated from line items on submit when lines are used.</summary>
        [StringLength(2000)]
        public string ItemDescription { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Justification is required.")]
        [StringLength(2000)]
        public string Justification { get; set; }

        public int? QuantityInStock { get; set; }

        public DateTime? RequiredDate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        [StringLength(10)]
        public string Currency { get; set; }

        [StringLength(2000)]
        public string Notes { get; set; }

        /// <summary>Optional existing asset to tag for easier assignment after purchase.</summary>
        public int? TargetAssetId { get; set; }

        public IList<PurchaseRequestLineCreateVm> Lines { get; set; } = new List<PurchaseRequestLineCreateVm>();

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Justification))
            {
                yield return new ValidationResult(
                    "Justification is required.",
                    new[] { "Justification" });
            }

            var effectiveLines = ResolveEffectiveLines();
            if (effectiveLines.Count == 0)
            {
                yield return new ValidationResult(
                    "Describe at least one item to order.",
                    new[] { "Lines" });
            }
            else
            {
                for (var i = 0; i < effectiveLines.Count; i++)
                {
                    if (effectiveLines[i].Quantity < 1)
                    {
                        yield return new ValidationResult(
                            "Quantity must be at least 1.",
                            new[] { "Lines[" + i + "].Quantity" });
                    }
                }
            }
        }

        public IList<PurchaseRequestLineCreateVm> ResolveEffectiveLines()
        {
            var fromLines = (Lines ?? new List<PurchaseRequestLineCreateVm>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Description))
                .Select(x => new PurchaseRequestLineCreateVm
                {
                    Description = x.Description.Trim(),
                    Quantity = x.Quantity > 0 ? x.Quantity : 1
                })
                .ToList();
            if (fromLines.Count > 0)
            {
                return fromLines;
            }

            if (!string.IsNullOrWhiteSpace(ItemDescription))
            {
                return new List<PurchaseRequestLineCreateVm>
                {
                    new PurchaseRequestLineCreateVm
                    {
                        Description = ItemDescription.Trim(),
                        Quantity = Quantity > 0 ? Quantity : 1
                    }
                };
            }

            return new List<PurchaseRequestLineCreateVm>();
        }
    }

    public class PurchaseRequestListItemVm
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string DepartmentName { get; set; }
        public DepartmentKind? DepartmentKind { get; set; }

        public string DepartmentKindLabel { get; set; }

        public string RequestedById { get; set; }

        public string ApprovalStatus { get; set; }

        public DateTime CreatedAt { get; set; }

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
        public DepartmentKind? DepartmentKind { get; set; }

        public string DepartmentKindLabel { get; set; }

        public string ItemDescription { get; set; }

        public string Justification { get; set; }

        public int? QuantityInStock { get; set; }

        public DateTime? RequiredDate { get; set; }

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

        public IList<PurchaseRequestLineVm> LineItems { get; set; } = new List<PurchaseRequestLineVm>();

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
