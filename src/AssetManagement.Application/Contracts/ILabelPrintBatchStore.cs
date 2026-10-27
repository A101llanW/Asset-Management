using System;
using System.Collections.Generic;

namespace AssetManagement.Application.Contracts
{
    public class LabelPrintAssetInfo
    {
        public int AssetId { get; set; }
        public string AssetTag { get; set; }
        public string AssetName { get; set; }

        /// <summary>Asset's department (for the department scope of the print log).</summary>
        public int? DepartmentId { get; set; }

        /// <summary>True when the asset's department is an active Class department.</summary>
        public bool DepartmentIsActiveClass { get; set; }
    }

    public class LabelPrintBatchCreate
    {
        public int OrganizationId { get; set; }
        public string Source { get; set; }
        public string GroupLabel { get; set; }
        public string FilterDescription { get; set; }
        public bool IsReprint { get; set; }
        public int? ReprintOfBatchId { get; set; }
        public string TemplateId { get; set; }
        public string CodeType { get; set; }
        public string CreatedById { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public IList<LabelPrintAssetInfo> Assets { get; set; } = new List<LabelPrintAssetInfo>();
    }

    public class LabelPrintBatchInfo
    {
        public int Id { get; set; }
        public int OrganizationId { get; set; }
        public string Source { get; set; }
        public string GroupLabel { get; set; }
        public string FilterDescription { get; set; }
        public int AssetCount { get; set; }
        public int PrintedCount { get; set; }
        public string Status { get; set; }
        public bool IsReprint { get; set; }
        public int? ReprintOfBatchId { get; set; }
        public string TemplateId { get; set; }
        public string CodeType { get; set; }
        public string CreatedById { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? LastPrintedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public bool IsCleared { get; set; }
        public string ClearedById { get; set; }
        public string ClearedByName { get; set; }
        public DateTime? ClearedAtUtc { get; set; }
    }

    public class LabelPrintBatchItemInfo
    {
        public int AssetId { get; set; }
        public string AssetTag { get; set; }
        public string AssetName { get; set; }
        public bool AssetIsActive { get; set; }
        public int? DepartmentId { get; set; }
        public bool DepartmentIsActiveClass { get; set; }
        public int PrintCount { get; set; }
        public DateTime? LastPrintedAtUtc { get; set; }
    }

    public class AssetPrintHistoryEntry
    {
        public int AuditLogId { get; set; }
        public string Action { get; set; }
        public bool IsReprint { get; set; }
        public DateTime TimestampUtc { get; set; }
        public string ActorUserId { get; set; }
        public string ActorName { get; set; }
        public string Details { get; set; }
    }

    public class AssetPrintHistory
    {
        public int PrintCount { get; set; }
        public int ReprintCount { get; set; }
        public DateTime? LastPrintedAtUtc { get; set; }
        public IList<AssetPrintHistoryEntry> Entries { get; set; } = new List<AssetPrintHistoryEntry>();
    }

    /// <summary>
    /// rel1022a (migration 105): SQL side of the label print log. Batches live in LabelPrintBatch / LabelPrintBatchItem;
    /// per-asset print history is read from AuditLog (Assets.LabelPrint / Assets.LabelReprint). Every call is tenant-scoped.
    /// </summary>
    public interface ILabelPrintBatchStore
    {
        /// <summary>Active assets of the tenant among <paramref name="assetIds"/> (unknown, inactive or other-tenant ids are dropped).</summary>
        IList<LabelPrintAssetInfo> GetPrintableAssets(int organizationId, ICollection<int> assetIds);

        /// <summary>Number of earlier print/reprint audit rows per asset (assets never printed are absent).</summary>
        IDictionary<int, int> GetPriorPrintCounts(int organizationId, ICollection<int> assetIds);

        /// <summary>Batch row plus its items in one transaction. Returns the new batch id.</summary>
        int CreateBatch(LabelPrintBatchCreate batch);

        LabelPrintBatchInfo GetBatch(int organizationId, int batchId);

        IList<LabelPrintBatchInfo> GetBatches(int organizationId, bool includeCleared, int take);

        IList<LabelPrintBatchItemInfo> GetBatchItems(int organizationId, int batchId);

        /// <summary>Counts one printed label in the batch (adds the asset to the batch if it was queued later).</summary>
        void MarkItemPrinted(int organizationId, int batchId, int assetId, string assetTag, DateTime printedAtUtc);

        void CompleteBatch(int organizationId, int batchId, bool stoppedEarly, DateTime completedAtUtc);

        /// <summary>Soft-hide: sets IsCleared/ClearedById/ClearedAt. Returns false when the batch was already cleared or not found.</summary>
        bool ClearBatch(int organizationId, int batchId, string clearedById, DateTime clearedAtUtc);

        AssetPrintHistory GetPrintHistory(int organizationId, int assetId, int take);
    }
}
