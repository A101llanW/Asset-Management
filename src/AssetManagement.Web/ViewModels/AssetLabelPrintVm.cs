namespace AssetManagement.Web.ViewModels
{
    public class AssetLabelPrintVm
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string DepartmentName { get; set; }

        public string SerialNumber { get; set; }

        public string CurrentStatus { get; set; }

        public string ScanUrl { get; set; }

        public string BarcodePayload { get; set; }

        /// <summary>Current organisation name (Organization.Name) for the Company name element.</summary>
        public string CompanyName { get; set; }

        /// <summary>Saved label templates the user can print with ("" = active layout).</summary>
        public System.Collections.Generic.IList<System.Web.Mvc.SelectListItem> TemplateOptions { get; set; }
    }
}
