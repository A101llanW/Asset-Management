using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Areas.Platform.Controllers
{
    [PermissionAuthorize("Platform.Organizations.View")]
    public class PlatformSettingsController : Controller
    {
        private readonly IPlatformSettingsService _platformSettings;

        public PlatformSettingsController()
        {
            _platformSettings = DependencyResolver.Current.GetService<IPlatformSettingsService>();
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
                return View(model);
            }

            if (_platformSettings == null)
            {
                TempData["Error"] = "Platform settings service is unavailable.";
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

        private PlatformEmailSettingsViewModel BuildViewModel()
        {
            if (_platformSettings == null)
            {
                return new PlatformEmailSettingsViewModel();
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

            return new PlatformEmailSettingsViewModel
            {
                SmtpHost = _platformSettings.GetSetting("SmtpHost", string.Empty),
                SmtpPort = port,
                SmtpUser = _platformSettings.GetSetting("SmtpUser", string.Empty),
                SmtpEnableSsl = enableSsl,
                FromEmail = _platformSettings.GetSetting("FromEmail", string.Empty),
                FromName = _platformSettings.GetSetting("FromName", "Asset Management Module"),
                ExternalBaseUrl = _platformSettings.GetExternalBaseUrl()
            };
        }
    }
}
