using System.Collections.Generic;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Web.ViewModels
{
    /// <summary>rel1013b: rename existing asset tags to the prefix format (preview, apply batch, undo batch).</summary>
    public class AssetTagRenameVm
    {
        /// <summary>Null = whole tenant; otherwise the department / sub-department / room and everything below it.</summary>
        public int? ScopeDepartmentId { get; set; }
        public string ScopeLabel { get; set; }
        public bool Previewed { get; set; }
        public bool StructuredTagsEnabled { get; set; }
        public int CandidateCount { get; set; }
        public int AlreadyConformingCount { get; set; }
        /// <summary>Fingerprint of the previewed asset ids + old tags; Apply refuses when the plan changed since.</summary>
        public string PlanFingerprint { get; set; }
        public bool Truncated { get; set; }
        public List<KeyValuePair<int, string>> ScopeOptions { get; set; } = new List<KeyValuePair<int, string>>();
        public List<AssetTagRenamePlanItem> Items { get; set; } = new List<AssetTagRenamePlanItem>();
        public List<AssetTagRenameBatchInfo> Batches { get; set; } = new List<AssetTagRenameBatchInfo>();
        public int? ViewBatchId { get; set; }
        public List<AssetTagRenamePlanItem> BatchItems { get; set; } = new List<AssetTagRenamePlanItem>();
        /// <summary>Three prefix boxes (blank = use each node's saved prefix or department code).</summary>
        public string DepartmentPrefix { get; set; }
        public string SubDepartmentPrefix { get; set; }
        public string RoomPrefix { get; set; }
        /// <summary>Comma-separated department ids whose code is unticked (left out of the tag).</summary>
        public string ExcludedCodes { get; set; }
        public List<AssetManagement.Application.Helpers.AssetTagRenamePlanner.LevelCode> LevelCodes { get; set; } = new List<AssetManagement.Application.Helpers.AssetTagRenamePlanner.LevelCode>();
        /// <summary>Example next tag (first planned rename) for the current boxes and ticks.</summary>
        public string NextTagExample { get; set; }
    }

    /// <summary>Shared three-level prefix preview (room / department / sub-department boxes, code columns with checkboxes, next tag). Used by AssetTags and AssetTagRename.</summary>
    public class AssetTagLevelPreviewVm
    {
        /// <summary>Id of the GET form the boxes and checkboxes belong to; ticking a code re-submits it.</summary>
        public string FormId { get; set; }
        public string DepartmentPrefix { get; set; }
        public string SubDepartmentPrefix { get; set; }
        public string RoomPrefix { get; set; }
        public string ExcludedCodes { get; set; }
        public bool Previewed { get; set; }
        public string NextTag { get; set; }
        public string CountLabel { get; set; } = "asset(s)";
        public List<AssetManagement.Application.Helpers.AssetTagRenamePlanner.LevelCode> Codes { get; set; } = new List<AssetManagement.Application.Helpers.AssetTagRenamePlanner.LevelCode>();
    }
}