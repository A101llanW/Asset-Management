using System.Collections.Generic;
using System.Web.Mvc;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Web.ViewModels
{
    public class AssetScanLookupPageVm
    {
        public AssetScanLookupVm Lookup { get; set; }

        public bool IsPublicScan { get; set; }

        public bool CanViewAssetDetails { get; set; }

        public bool CanOpenQuickActions { get; set; }

        public bool CanBatchPrintLabels { get; set; }

        public string StatusBadgeClass { get; set; }

        public string DetailsUrl { get; set; }

        public string QuickActionsUrl { get; set; }

        public string BrandModelDisplay { get; set; }

        public string LookupJsonUrl { get; set; }

        /// <summary>Link to the separate bulk label print page; null when the user cannot print labels.</summary>
        public string BulkPrintUrl { get; set; }

        public string PrintCandidatesUrl { get; set; }

        /// <summary>rel1014a: PO filter options on Bulk Label Print (tenant purchases).</summary>
        public System.Collections.Generic.IEnumerable<System.Web.Mvc.SelectListItem> PurchaseOptions { get; set; }

        /// <summary>rel1014a: purchase preselected from ?purchaseId= (Send to Bulk Label Print); the page auto-loads it.</summary>
        public int? InitialPurchaseId { get; set; }

        public string InitialPurchaseLabel { get; set; }

        public string LabelPrintConfigUrl { get; set; }

        public string LabelZplUrlTemplate { get; set; }

        /// <summary>Saved label templates offered on Bulk Label Print ("" = active layout).</summary>
        public IList<SelectListItem> LabelTemplateOptions { get; set; }

        /// <summary>Category / type / sub-type options for the Bulk Label Print cascading filters.</summary>
        public AssetManagement.Web.Helpers.ClassificationCatalog ClassificationCatalog { get; set; }

        public string InitialCode { get; set; }

        public GlobalSearchResultVm SearchResults { get; set; }

        public IEnumerable<SelectListItem> DepartmentOptions { get; set; }

        public IEnumerable<SelectListItem> StatusOptions { get; set; }
    }
}
