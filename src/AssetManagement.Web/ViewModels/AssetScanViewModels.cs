using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.ViewModels
{
    public class AssetScanLookupPageVm
    {
        public AssetScanLookupVm Lookup { get; set; }

        public bool IsPublicScan { get; set; }

        public bool CanViewAssetDetails { get; set; }

        public bool CanOpenQuickActions { get; set; }

        public string StatusBadgeClass { get; set; }

        public string DetailsUrl { get; set; }

        public string QuickActionsUrl { get; set; }

        public string BrandModelDisplay { get; set; }

        public string LookupJsonUrl { get; set; }

        public string InitialCode { get; set; }
    }
}
