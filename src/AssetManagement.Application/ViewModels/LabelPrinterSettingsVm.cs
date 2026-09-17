using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Application.ViewModels
{
    public class LabelPrinterSettingsVm
    {
        [Display(Name = "Enable Zebra label printer")]
        public bool Enabled { get; set; }

        [StringLength(100)]
        [Display(Name = "Printer model")]
        public string Model { get; set; }

        [StringLength(50)]
        [Display(Name = "Print mode")]
        public string Mode { get; set; }

        [StringLength(200)]
        [Display(Name = "Windows printer name (optional)")]
        public string DeviceName { get; set; }

        [Range(15, 104)]
        [Display(Name = "Label width (mm)")]
        public int WidthMm { get; set; }

        [Range(6, 279)]
        [Display(Name = "Label height (mm)")]
        public int HeightMm { get; set; }

        [Range(typeof(decimal), "0.5", "10")]
        [Display(Name = "QR magnification")]
        public decimal QrMagnification { get; set; }

        [StringLength(50)]
        [Display(Name = "Layout preset")]
        public string LayoutPreset { get; set; }

        [Display(Name = "Custom layout design")]
        public string LayoutDesignJson { get; set; }
    }
}
