using System.Collections.Generic;

namespace AssetManagement.Web.ViewModels
{
    public class AssetTagSettingsVm
    {
        public bool Enabled { get; set; } = true;
        public string TenantPrefix { get; set; }
        /// <summary>Optional prefix for Prep units (migration 106). Blank = no section prefix: nothing changes for Prep.</summary>
        public string PrepPrefix { get; set; }
        /// <summary>Optional prefix for Senior school units. Blank = no section prefix: nothing changes for Senior school.</summary>
        public string SeniorPrefix { get; set; }

        /// <summary>rel1018b tenant defaults for the per-level segment switches.</summary>
        public bool UseDepartmentSegment { get; set; } = true;
        public bool UseSubDepartmentSegment { get; set; }
        public bool UseRoomSegment { get; set; } = true;

        public List<AssetTagOverrideRowVm> Rows { get; set; } = new List<AssetTagOverrideRowVm>();
        /// <summary>Three-level prefix preview (read-only; does not change the saved settings).</summary>
        public int? LevelScopeId { get; set; }
        public AssetTagLevelPreviewVm Level { get; set; } = new AssetTagLevelPreviewVm { FormId = "levelPreviewForm", CountLabel = "unit(s)" };
    }

    public class AssetTagOverrideRowVm
    {
        public int DepartmentId { get; set; }
        public int? ParentDepartmentId { get; set; }
        public string Label { get; set; }
        public string KindLabel { get; set; }
        public int Depth { get; set; }
        public bool IsRoom { get; set; }
        public string Override { get; set; }

        /// <summary>rel1018b per-node switches as posted by the tri-state selects: "" = inherit, "true", "false".</summary>
        public string UseDepartment { get; set; }
        public string UseSubDepartment { get; set; }
        public string UseRoom { get; set; }

        public string EffectivePrefix { get; set; }
        public string EffectiveSource { get; set; }
    }
}
