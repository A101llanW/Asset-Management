namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Code-level UI feature switches (not Web.config). Flip a flag to restore chrome without redeploying config.
    /// </summary>
    public static class FeatureFlags
    {
        /// <summary>
        /// When false, Excel asset-import UI entry points are hidden and Import/DownloadImportTemplate actions redirect to Assets/Index.
        /// Controllers, views, and services remain in place for later restore.
        /// </summary>
        // static readonly, not const: a const false makes Razor views using the flag fail to compile on the live
        // site (CS0429/CS0162 unreachable code reported as errors at runtime view compilation).
        public static readonly bool AssetExcelImportUiEnabled = false;
    }
}