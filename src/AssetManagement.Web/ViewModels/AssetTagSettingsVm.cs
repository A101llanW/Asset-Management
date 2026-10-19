using System.Collections.Generic;

namespace AssetManagement.Web.ViewModels
{
    public class AssetTagSettingsVm
    {
        public bool Enabled { get; set; } = true;
        public string TenantPrefix { get; set; }
        public List<AssetTagOverrideRowVm> Rows { get; set; } = new List<AssetTagOverrideRowVm>();
        /// <summary>Three-level prefix preview (read-only; does not change the saved settings).</summary>
        public int? LevelScopeId { get; set; }
        public AssetTagLevelPreviewVm Level { get; set; } = new AssetTagLevelPreviewVm { FormId = "levelPreviewForm", CountLabel = "unit(s)" };
    }

    public class AssetTagOverrideRowVm
    {
        public int DepartmentId { get; set; }
        public string Label { get; set; }
        public string KindLabel { get; set; }
        public int Depth { get; set; }
        public bool IsRoom { get; set; }
        public string Override { get; set; }
        public string EffectivePrefix { get; set; }
        public string EffectiveSource { get; set; }
    }
}