using System.ComponentModel.DataAnnotations;

namespace AssetManagement.Web.ViewModels
{
    /// <summary>
    /// Settings &gt; Email (SMTP) for one company. <see cref="NewPassword"/> is write-only: it is never
    /// rendered back, blank keeps the saved password and <see cref="ClearPassword"/> removes it.
    /// </summary>
    public class CompanySmtpSettingsViewModel
    {
        public CompanySmtpSettingsViewModel()
        {
            Port = 587;
            UseSsl = true;
        }

        [Display(Name = "Use own SMTP server for this company")]
        public bool IsEnabled { get; set; }

        [Display(Name = "SMTP host")]
        [StringLength(255)]
        public string Host { get; set; }

        [Display(Name = "Port")]
        [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535.")]
        public int Port { get; set; }

        [Display(Name = "Use SSL/TLS")]
        public bool UseSsl { get; set; }

        [Display(Name = "Username")]
        [StringLength(256)]
        public string Username { get; set; }

        [Display(Name = "Password")]
        [DataType(DataType.Password)]
        [StringLength(256)]
        public string NewPassword { get; set; }

        [Display(Name = "Clear the saved password")]
        public bool ClearPassword { get; set; }

        [Display(Name = "From address")]
        [StringLength(256)]
        public string FromAddress { get; set; }

        [Display(Name = "From display name")]
        [StringLength(200)]
        public string FromDisplayName { get; set; }

        // Display only (rebuilt on every render; never trusted from the form).
        public bool StoreAvailable { get; set; }

        public bool HasStoredPassword { get; set; }

        public string OrganizationName { get; set; }

        public string CurrentRouteLabel { get; set; }

        public string CurrentRouteDetail { get; set; }

        public bool UsesOwnSmtp { get; set; }

        public bool CanSendTest { get; set; }

        public string TestRecipient { get; set; }

        public string LastUpdatedLabel { get; set; }
    }
}
