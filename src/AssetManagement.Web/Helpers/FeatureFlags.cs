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
        public const bool AssetExcelImportUiEnabled = false;
    }
}