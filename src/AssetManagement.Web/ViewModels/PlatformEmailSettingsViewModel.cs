using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Web.ViewModels
{
    public class PlatformEmailSettingsViewModel
    {
        [Display(Name = "SMTP host")]
        public string SmtpHost { get; set; }

        [Display(Name = "SMTP port")]
        public int SmtpPort { get; set; }

        [Display(Name = "SMTP username")]
        public string SmtpUser { get; set; }

        [Display(Name = "SMTP password")]
        [DataType(DataType.Password)]
        public string SmtpPassword { get; set; }

        [Display(Name = "Enable SSL")]
        public bool SmtpEnableSsl { get; set; }

        [Display(Name = "From email")]
        public string FromEmail { get; set; }

        [Display(Name = "From name")]
        public string FromName { get; set; }

        [Display(Name = "External base URL")]
        public string ExternalBaseUrl { get; set; }
    }
}
