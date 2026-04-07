namespace AssetManagement.Web.ViewModels
{
    public class PageHeaderViewModel
    {
        public string Title { get; set; }

        public string Subtitle { get; set; }

        public string BackUrl { get; set; }

        public string BackLabel { get; set; }

        public string PrimaryActionUrl { get; set; }

        public string PrimaryActionLabel { get; set; }
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
