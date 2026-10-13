using System;
using System.Collections.Generic;

namespace AssetManagement.Application.Contracts
{
    public class AssetTagRenameCandidate
    {
        public int AssetId { get; set; }
        public string AssetTag { get; set; }
        public string AssetName { get; set; }
        public int? DepartmentId { get; set; }
    }

    public class AssetTagRenamePlanItem
    {
        public int AssetId { get; set; }
        public string AssetName { get; set; }
        public int? DepartmentId { get; set; }
        public string OldTag { get; set; }
        public string Prefix { get; set; }
        /// <summary>Predicted new tag (preview). The applied tag is allocated inside the batch transaction and is normally the same.</summary>
        public string NewTag { get; set; }
    }

    public class AssetTagRenameBatchInfo
    {
        public int Id { get; set; }
        public string ScopeLabel { get; set; }
        public int AssetCount { get; set; }
        public string Status { get; set; }
        public string AppliedById { get; set; }
        public DateTime AppliedAt { get; set; }
        public DateTime? UndoneAt { get; set; }
    }

    public class AssetTagRenameOutcome
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int BatchId { get; set; }
        public List<AssetTagRenamePlanItem> Items { get; set; } = new List<AssetTagRenamePlanItem>();
    }

    /// <summary>
    /// rel1013b (migration 103): SQL store for renaming existing tags in batches. Apply and Undo each run in ONE SqlTransaction
    /// under an exclusive per-tenant app lock, allocate numbers from AssetTagSequence inside that transaction, and check every
    /// new tag against all tenant asset tags and active aliases. Any conflict rolls the whole batch back.
    /// </summary>
    public interface IAssetTagRenameStore
    {
        IList<AssetTagRenameCandidate> GetCandidates(int organizationId, ICollection<int> departmentIds, bool includeUnassigned);

        /// <summary>Tags already used with PREFIX- in the tenant: asset tags (any state) and active aliases.</summary>
        ISet<string> GetTakenTags(int organizationId, string prefix);

        int PeekNext(int organizationId, string prefix);

        AssetTagRenameOutcome Apply(int organizationId, string actorUserId, int? scopeDepartmentId, string scopeLabel, IList<AssetTagRenamePlanItem> items);

        AssetTagRenameOutcome Undo(int organizationId, int batchId, string actorUserId);

        IList<AssetTagRenameBatchInfo> GetBatches(int organizationId, int take);

        IList<AssetTagRenamePlanItem> GetBatchItems(int organizationId, int batchId);

        /// <summary>True when the tag is an active alias of some asset in the tenant (used by manual-tag uniqueness).</summary>
        bool AliasExists(int organizationId, string tag);
    }
}