using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class PurchaseRequestLineCreateVm
    {
        /// <summary>
        /// "Item / asset name" (migration 108): becomes the received asset's name. Required on untagged lines; a tagged
        /// line defaults to the tagged asset's name (editable). Limit: AssetNameRules.MaxLength.
        /// </summary>
        [Display(Name = "Item / asset name")]
        [StringLength(AssetNameRules.MaxLength, ErrorMessage = "Item / asset name cannot be longer than {1} characters.")]
        public string ItemName { get; set; }

        [StringLength(2000)]
        public string Description { get; set; }

        /// <summary>
        /// Existing asset tagged on this line (migration 108). Supplies the line's classification and pre-fills the
        /// name; receiving still creates a NEW asset and never changes the tagged one.
        /// </summary>
        public int? TargetAssetId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1.")]
        public int Quantity { get; set; } = 1;

        /// <summary>Optional catalog classification (catalog requisition flows); validated and normalized on submit.</summary>
        public int? AssetCategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }
    }

    public class PurchaseRequestLineVm
    {
        /// <summary>Most specific catalog pick, e.g. "Laptops > Dell Latitude 5440" (null = unclassified).</summary>
        public string ClassificationLabel { get; set; }

        public int LineNumber { get; set; }

        /// <summary>NULL on lines created before migration 108.</summary>
        public string ItemName { get; set; }

        public string Description { get; set; }

        public int Quantity { get; set; }

        public int? AssetCategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public int? AssetSubTypeId { get; set; }

        /// <summary>Per-line tagged asset (migration 108); NULL on older lines (the header tag then applies).</summary>
        public int? TargetAssetId { get; set; }

        public string TargetAssetTag { get; set; }

        public string TargetAssetName { get; set; }
    }

    /// <summary>Outcome of a requisition submit: one id, or several linked by a group when lines needed different paths.</summary>
    public class PurchaseRequestSubmitResult
    {
        public int? GroupId { get; set; }

        public string BatchLabel { get; set; }

        public IList<int> RequestIds { get; } = new List<int>();

        public IList<string> RequestNumbers { get; } = new List<string>();

        public IList<string> FlowSources { get; } = new List<string>();

        public int PrimaryId
        {
            get { return RequestIds.Count == 0 ? 0 : RequestIds[0]; }
        }

        public bool IsSplit
        {
            get { return RequestIds.Count > 1; }
        }
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

        /// <summary>Legacy single-line "Item / asset name" (used only when no Lines are posted).</summary>
        [StringLength(AssetNameRules.MaxLength)]
        public string ItemName { get; set; }

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

        /// <summary>
        /// Back-compat header tag (older forms / links). When no line carries its own TargetAssetId it is applied to
        /// line 1. New forms tag per line (PurchaseRequestLineCreateVm.TargetAssetId).
        /// </summary>
        public int? TargetAssetId { get; set; }

        public IList<PurchaseRequestLineCreateVm> Lines { get; set; } = new List<PurchaseRequestLineCreateVm>();

        /// <summary>Header classification used when the org's ClassificationMode is RequiredHeader (lines without their own pick inherit it).</summary>
        public int? HeaderAssetCategoryId { get; set; }

        public int? HeaderAssetTypeId { get; set; }

        public int? HeaderAssetSubTypeId { get; set; }

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

                    // Mirrors the new-line rules in PurchaseRequestService.SubmitBatch (which re-checks everything).
                    var tagged = effectiveLines[i].TargetAssetId.HasValue || (i == 0 && TargetAssetId.HasValue && TargetAssetId.Value > 0);
                    if (!tagged && string.IsNullOrWhiteSpace(effectiveLines[i].ItemName))
                    {
                        yield return new ValidationResult(
                            "Line " + (i + 1) + ": enter the item / asset name.",
                            new[] { "Lines[" + i + "].ItemName" });
                    }

                    // A line without its own type may inherit the header type / sub-type (RequiredHeader mode).
                    var lineHasType = effectiveLines[i].AssetTypeId.HasValue && effectiveLines[i].AssetTypeId.Value > 0;
                    var hasSubType = (effectiveLines[i].AssetSubTypeId.HasValue && effectiveLines[i].AssetSubTypeId.Value > 0)
                        || (!lineHasType && HeaderAssetSubTypeId.HasValue && HeaderAssetSubTypeId.Value > 0);
                    if (!tagged && !hasSubType)
                    {
                        yield return new ValidationResult(
                            "Line " + (i + 1) + ": choose the asset type and sub-type.",
                            new[] { "Lines[" + i + "].AssetSubTypeId" });
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
                    ItemName = AssetNameRules.Normalize(x.ItemName),
                    Description = x.Description.Trim(),
                    Quantity = x.Quantity > 0 ? x.Quantity : 1,
                    AssetCategoryId = x.AssetCategoryId,
                    AssetTypeId = x.AssetTypeId,
                    AssetSubTypeId = x.AssetSubTypeId,
                    TargetAssetId = x.TargetAssetId.HasValue && x.TargetAssetId.Value > 0 ? x.TargetAssetId : null
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
                        ItemName = AssetNameRules.Normalize(ItemName),
                        Description = ItemDescription.Trim(),
                        Quantity = Quantity > 0 ? Quantity : 1,
                        AssetCategoryId = HeaderAssetCategoryId,
                        AssetTypeId = HeaderAssetTypeId,
                        AssetSubTypeId = HeaderAssetSubTypeId
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

        /// <summary>Tagged asset shown in the Requisitions list ("Tagged asset" column): the header TargetAssetId.</summary>
        public int? TargetAssetId { get; set; }

        public string TargetAssetTag { get; set; }

        public string TargetAssetName { get; set; }
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

        /// <summary>False when a "Purchase order raised by" is configured for this requisition's flow and the current user is not it.</summary>
        public bool CanCurrentUserRaisePurchaseOrder { get; set; } = true;

        /// <summary>Configured PO raiser label (null = not configured: anyone with Purchases permission).</summary>
        public string PurchaseOrderRaiserLabel { get; set; }

        public bool IsPending { get; set; }

        public bool IsApproved { get; set; }

        public bool HasPurchaseRecord { get; set; }

        public int? LinkedPurchaseRecordId { get; set; }

        public int? TargetAssetId { get; set; }

        public string TargetAssetTag { get; set; }

        public string TargetAssetName { get; set; }

        public IList<PurchaseRequestLineVm> LineItems { get; set; } = new List<PurchaseRequestLineVm>();

        /// <summary>Set when this requisition was auto-split from one form with siblings.</summary>
        public int? PurchaseRequestGroupId { get; set; }

        /// <summary>Winning approval source label at submit (null for requisitions created before migration 100).</summary>
        public string ApprovalFlowSource { get; set; }

        /// <summary>BATCH-000123 when auto-split (see <see cref="PurchaseRequestGroupId"/>).</summary>
        public string BatchLabel { get; set; }

        /// <summary>Other requisitions from the same form that the current user can open.</summary>
        public IList<PurchaseRequestSiblingVm> Siblings { get; set; } = new List<PurchaseRequestSiblingVm>();

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
