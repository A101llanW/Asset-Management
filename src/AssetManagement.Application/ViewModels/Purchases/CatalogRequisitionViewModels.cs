using System;
using System.Collections.Generic;
using System.Linq;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>Active catalog nodes for the requisition line pickers (tenant-filtered).</summary>
    public class CatalogPickerVm
    {
        public IList<CatalogPickerItemVm> Categories { get; set; } = new List<CatalogPickerItemVm>();

        /// <summary>ParentId = AssetCategoryId.</summary>
        public IList<CatalogPickerItemVm> Types { get; set; } = new List<CatalogPickerItemVm>();

        /// <summary>ParentId = AssetTypeId.</summary>
        public IList<CatalogPickerItemVm> SubTypes { get; set; } = new List<CatalogPickerItemVm>();

        /// <summary>
        /// Rule C5 (Allan 2026-10-27, "should not be the case" 2026-10-28 09:33 EAT): a type with no active sub-types
        /// cannot be requisitioned on an untagged line, so it is not offered. Returns a copy without those types
        /// (categories and sub-types are kept; every kept sub-type belongs to a kept type). Tagged lines take their
        /// classification from the tagged asset and never use this list.
        /// </summary>
        public CatalogPickerVm RequisitionableOnly()
        {
            var typeIdsWithSubTypes = TypeIdsWithSubTypes();
            return new CatalogPickerVm
            {
                Categories = new List<CatalogPickerItemVm>(Categories ?? new List<CatalogPickerItemVm>()),
                Types = (Types ?? new List<CatalogPickerItemVm>()).Where(x => x != null && typeIdsWithSubTypes.Contains(x.Id)).ToList(),
                SubTypes = new List<CatalogPickerItemVm>(SubTypes ?? new List<CatalogPickerItemVm>())
            };
        }

        /// <summary>Ids of the listed types that have at least one listed (active) sub-type.</summary>
        public ISet<int> TypeIdsWithSubTypes()
        {
            return new HashSet<int>((SubTypes ?? new List<CatalogPickerItemVm>())
                .Where(x => x != null && x.ParentId.HasValue)
                .Select(x => x.ParentId.Value));
        }
    }

    public class CatalogPickerItemVm
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int? ParentId { get; set; }
    }

    /// <summary>Admin editor for the requisition approval path on one catalog node.</summary>
    public class CatalogRequisitionFlowEditVm
    {
        public CatalogFlowScope Scope { get; set; }

        public int ScopedEntityId { get; set; }

        public string ScopedEntityName { get; set; }

        /// <summary>"Category &gt; Type &gt; Sub-type" breadcrumb of the node.</summary>
        public string PathLabel { get; set; }

        public RequisitionFlowMode FlowMode { get; set; }

        public IList<ApprovalStageSettingsVm> Stages { get; set; } = new List<ApprovalStageSettingsVm>();

        public int? PurchaseOrderRaiserRoleId { get; set; }

        public string PurchaseOrderRaiserUserId { get; set; }

        /// <summary>True when a row exists for this node (any mode).</summary>
        public bool Exists { get; set; }

        /// <summary>Stored stage role ids (from the DB, never the post) for StageApproverRules legacy tolerance.</summary>
        public IList<int> StoredStageRoleIds { get; set; } = new List<int>();

        public int? StoredPurchaseOrderRaiserRoleId { get; set; }

        /// <summary>What applies when this node inherits: the nearest configured ancestor, else "Department flow".</summary>
        public string InheritedFromLabel { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    public class CatalogRequisitionFlowListItemVm
    {
        public CatalogFlowScope Scope { get; set; }

        public int ScopedEntityId { get; set; }

        public string ScopedEntityName { get; set; }

        public string PathLabel { get; set; }

        public RequisitionFlowMode FlowMode { get; set; }

        public IList<int> StageRoleIds { get; set; } = new List<int>();

        public IList<string> StageUserIds { get; set; } = new List<string>();

        public int? PurchaseOrderRaiserRoleId { get; set; }

        public string PurchaseOrderRaiserUserId { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>Settings &gt; Requisition policies (org settings, seeded lazily on first save).</summary>
    public class RequisitionPolicySettingsVm
    {
        public RequisitionClassificationMode ClassificationMode { get; set; }

        public RequisitionMixedFlowPolicy MixedFlowPolicy { get; set; }

        /// <summary>False until the org has saved the screen once (defaults apply).</summary>
        public bool IsStored { get; set; }
    }

    /// <summary>Create form: live per-line approval path preview, computed with the same rules as submit.</summary>
    public class PurchaseRequestPathPreviewVm
    {
        /// <summary>First validation error submit would raise (classification policy, forged ids); null = valid.</summary>
        public string Error { get; set; }

        public RequisitionClassificationMode ClassificationMode { get; set; }

        public RequisitionMixedFlowPolicy MixedFlowPolicy { get; set; }

        public IList<PurchaseRequestPathPreviewLineVm> Lines { get; set; } = new List<PurchaseRequestPathPreviewLineVm>();

        public IList<PurchaseRequestPathPreviewGroupVm> Groups { get; set; } = new List<PurchaseRequestPathPreviewGroupVm>();

        public bool IsMixed
        {
            get { return Groups != null && Groups.Count > 1; }
        }

        /// <summary>Mixed paths under Block: submit will be rejected.</summary>
        public bool WillBlock
        {
            get { return IsMixed && MixedFlowPolicy == RequisitionMixedFlowPolicy.Block; }
        }
    }

    public class PurchaseRequestPathPreviewLineVm
    {
        /// <summary>1-based position among the posted lines (blank lines included, so it matches the form rows).</summary>
        public int LineNumber { get; set; }

        public int GroupNumber { get; set; }

        public string SourceLabel { get; set; }
    }

    public class PurchaseRequestPathPreviewGroupVm
    {
        public int GroupNumber { get; set; }

        public string SourceLabel { get; set; }

        public string SourceKind { get; set; }

        public ApprovalProcessConfiguration Configuration { get; set; }

        public IList<int> LineNumbers { get; set; } = new List<int>();
    }

    /// <summary>Batch summary for requisitions auto-split from one form (PurchaseRequestGroup).</summary>
    public class PurchaseRequestBatchVm
    {
        public int GroupId { get; set; }

        public string BatchLabel { get; set; }

        public DateTime CreatedAt { get; set; }

        public string CreatedByUserId { get; set; }

        public string CreatedByName { get; set; }

        /// <summary>Siblings the current user cannot open are counted, not listed.</summary>
        public int HiddenCount { get; set; }

        public IList<PurchaseRequestBatchItemVm> Requests { get; set; } = new List<PurchaseRequestBatchItemVm>();
    }

    public class PurchaseRequestBatchItemVm
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string ApprovalStatus { get; set; }

        public int CurrentApprovalStage { get; set; }

        public int StageCount { get; set; }

        public string ApprovalFlowSource { get; set; }

        public string ItemSummary { get; set; }

        public int LineCount { get; set; }

        public int TotalQuantity { get; set; }

        public string DepartmentName { get; set; }

        public bool HasPurchaseRecord { get; set; }
    }

    public class PurchaseRequestSiblingVm
    {
        public int Id { get; set; }

        public string RequestNumber { get; set; }

        public string ApprovalFlowSource { get; set; }
    }
}
