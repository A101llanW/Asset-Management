namespace AssetManagement.Web.ViewModels
{
    public class BreadcrumbItemViewModel
    {
        public string Label { get; set; }

        public string Url { get; set; }
    }

    public class PageHeaderViewModel
    {
        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string BackUrl { get; set; }

        public string BackLabel { get; set; }

        public string PrimaryActionUrl { get; set; }

        public string PrimaryActionLabel { get; set; }

        public string SecondaryActionUrl { get; set; }

        public string SecondaryActionLabel { get; set; }

        public string TertiaryActionUrl { get; set; }

        public string TertiaryActionLabel { get; set; }

        /// <summary>When set, primary action opens this Bootstrap modal instead of navigating away.</summary>
        public string PrimaryActionModalTarget { get; set; }

        public System.Collections.Generic.IEnumerable<BreadcrumbItemViewModel> Breadcrumbs { get; set; }
    }

    public class DepartmentChildListViewModel
    {
        public string Title { get; set; }

        public string EmptyMessage { get; set; }

        public string ReturnUrl { get; set; }

        public string ListDomId { get; set; }

        public int InitialVisibleCount { get; set; }

        public System.Collections.Generic.IList<AssetManagement.Application.ViewModels.DepartmentVm> Items { get; set; }
    }

    public class FormActionsViewModel
    {
        public string SubmitLabel { get; set; }

        public string BackUrl { get; set; }

        public string BackLabel { get; set; }
    }

    public class AssetWorkflowContextViewModel
    {
        public int AssetId { get; set; }

        public string AssetName { get; set; }

        public string AssetTag { get; set; }

        public string Status { get; set; }

        public string DepartmentName { get; set; }

        public string CustodianName { get; set; }
    }
}
