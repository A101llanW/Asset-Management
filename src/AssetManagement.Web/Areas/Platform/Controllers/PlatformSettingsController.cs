using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Security;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Areas.Platform.Controllers
{
    [PermissionAuthorize("Platform.Organizations.View")]
    public class PlatformSettingsController : Controller
    {
        private readonly IPlatformSettingsService _platformSettings;
        private readonly IEmailService _emailService;

        public PlatformSettingsController()
        {
            _platformSettings = DependencyResolver.Current.GetService<IPlatformSettingsService>();
            _emailService = DependencyResolver.Current.GetService<IEmailService>();
        }

        [HttpGet]
        public ActionResult Email()
        {
            return View(BuildViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Email(PlatformEmailSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ApplyConfigurationStatus(model);
                return View(model);
            }

            if (_platformSettings == null)
            {
                TempData["Error"] = "Platform settings service is unavailable.";
                ApplyConfigurationStatus(model);
                return View(model);
            }

            _platformSettings.SetSetting("SmtpHost", model.SmtpHost, "SMTP server host");
            _platformSettings.SetSetting("SmtpPort", model.SmtpPort.ToString(), "SMTP server port");
            _platformSettings.SetSetting("SmtpUser", model.SmtpUser, "SMTP username");
            if (!string.IsNullOrWhiteSpace(model.SmtpPassword))
            {
                _platformSettings.SetSetting("SmtpPassword", model.SmtpPassword, "SMTP password");
            }

            _platformSettings.SetSetting("SmtpEnableSsl", model.SmtpEnableSsl.ToString(), "Enable SSL for SMTP");
            _platformSettings.SetSetting("FromEmail", model.FromEmail, "Outbound from email address");
            _platformSettings.SetSetting("FromName", model.FromName, "Outbound from display name");
            _platformSettings.SetSetting("ExternalBaseUrl", model.ExternalBaseUrl, "Public site base URL for password reset links");

            TempData["Message"] = "Email settings saved.";
            return RedirectToAction("Email");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendTestEmail(PlatformEmailSettingsViewModel model)
        {
            if (_emailService == null)
            {
                TempData["Error"] = "Email service is unavailable.";
                return RedirectToAction("Email");
            }

            string errorMessage;
            if (_emailService.SendTestEmail(model.TestRecipientEmail, out errorMessage))
            {
                TempData["Message"] = "Test email sent to " + model.TestRecipientEmail + ".";
            }
            else
            {
                TempData["Error"] = string.IsNullOrWhiteSpace(errorMessage)
                    ? "Test email could not be sent."
                    : errorMessage;
            }

            return RedirectToAction("Email");
        }

        private PlatformEmailSettingsViewModel BuildViewModel()
        {
            if (_platformSettings == null)
            {
                return ApplyConfigurationStatus(new PlatformEmailSettingsViewModel());
            }

            int port;
            if (!int.TryParse(_platformSettings.GetSetting("SmtpPort", "587"), out port))
            {
                port = 587;
            }

            bool enableSsl;
            if (!bool.TryParse(_platformSettings.GetSetting("SmtpEnableSsl", "true"), out enableSsl))
            {
                enableSsl = true;
            }

            return ApplyConfigurationStatus(new PlatformEmailSettingsViewModel
            {
                SmtpHost = _platformSettings.GetSetting("SmtpHost", string.Empty),
                SmtpPort = port,
                SmtpUser = _platformSettings.GetSetting("SmtpUser", string.Empty),
                SmtpEnableSsl = enableSsl,
                FromEmail = _platformSettings.GetSetting("FromEmail", string.Empty),
                FromName = _platformSettings.GetSetting("FromName", "Asset Management Module"),
                ExternalBaseUrl = _platformSettings.GetExternalBaseUrl()
            });
        }

        private PlatformEmailSettingsViewModel ApplyConfigurationStatus(PlatformEmailSettingsViewModel model)
        {
            if (model == null)
            {
                model = new PlatformEmailSettingsViewModel();
            }

            if (_emailService == null)
            {
                model.ConfigurationSummary = "Email service is unavailable.";
                model.ConfigurationIsReady = false;
                model.ConfigurationIsBlocking = DeploymentSecuritySettings.RequiresSmtpForAuthEmails;
                return model;
            }

            var status = _emailService.GetConfigurationStatus();
            model.ConfigurationSummary = status.GetSummary();
            model.ConfigurationIsReady = status.IsReadyForAuthDelivery;
            model.ConfigurationIsBlocking = status.IsBlockingProductionAuth;
            return model;
        }
    }
}
