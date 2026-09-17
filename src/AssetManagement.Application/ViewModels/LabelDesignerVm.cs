using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class LabelDesignerVm
    {
        public LabelPrinterSettingsVm LabelPrinter { get; set; } = new LabelPrinterSettingsVm();

        [Display(Name = "Layout design (JSON)")]
        public string LayoutDesignJson { get; set; }

        public string SampleAssetTag { get; set; } = "AST-1001";

        public string SampleAssetName { get; set; } = "Sample Laptop";

        public string SampleDepartmentName { get; set; } = "IT Department";

        public string SampleSerialNumber { get; set; } = "SN-123456";

        public string LayoutTemplatesJson { get; set; } = "[]";
    }
}
