using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Email;
using AssetManagement.Application.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Controllers
{
    /// <summary>
    /// Settings &gt; Email (SMTP): per-company outgoing email. Same Settings.Manage gate as the rest of
    /// SettingsController (Company Admin). The password is write-only and never logged or audited.
    /// </summary>
    public partial class SettingsController
    {
        [HttpGet]
        public ActionResult Email()
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                TempData["Error"] = "Open Settings inside a company to manage its email (SMTP) settings.";
                return RedirectToAction("Index");
            }

            return View(BuildSmtpViewModel(organizationId.Value, null));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Email")]
        public ActionResult EmailPost(CompanySmtpSettingsViewModel model)
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                TempData["Error"] = "Open Settings inside a company to manage its email (SMTP) settings.";
                return RedirectToAction("Index");
            }

            model = model ?? new CompanySmtpSettingsViewModel();
            var store = ResolveSmtpStore();
            if (store == null || !store.IsAvailable())
            {
                TempData["Error"] = "Company email settings are not available yet (database update 094 has not been applied). Emails use the platform default.";
                return RedirectToAction("Email");
            }

            var before = store.Get(organizationId.Value);
            var after = before == null
                ? new OrganizationSmtpSettingsRecord { OrganizationId = organizationId.Value }
                : before.Clone();
            after.OrganizationId = organizationId.Value;
            after.Host = TrimOrNull(model.Host);
            after.Port = model.Port;
            after.UseSsl = model.UseSsl;
            after.Username = TrimOrNull(model.Username);
            after.FromAddress = TrimOrNull(model.FromAddress);
            after.FromDisplayName = TrimOrNull(model.FromDisplayName);
            after.IsEnabled = model.IsEnabled;
            after.IsActive = true;

            var passwordChange = SmtpSettingsAudit.PasswordUnchanged;
            if (model.ClearPassword)
            {
                if (after.HasPassword)
                {
                    passwordChange = SmtpSettingsAudit.PasswordCleared;
                }

                after.PasswordEncrypted = null;
            }
            else if (!string.IsNullOrEmpty(model.NewPassword))
            {
                after.PasswordEncrypted = ResolveSecretProtector().Protect(model.NewPassword);
                passwordChange = SmtpSettingsAudit.PasswordChanged;
            }

            if (after.FromAddress != null && !SmtpSettingsResolution.IsValidEmailAddress(after.FromAddress))
            {
                ModelState.AddModelError("FromAddress", "Enter a valid From address (for example noreply@yourcompany.com).");
            }

            if (after.IsEnabled)
            {
                var reason = SmtpSettingsResolution.GetIncompleteReason(after, true);
                if (reason != null)
                {
                    ModelState.AddModelError(string.Empty, reason + " Complete it, or untick \"Use own SMTP server for this company\" to keep the platform default.");
                }
            }

            if (!ModelState.IsValid)
            {
                return View("Email", BuildSmtpViewModel(organizationId.Value, model));
            }

            store.Save(after, User.GetUserId());
            if (AuditWriter != null)
            {
                AuditWriter.Write(
                    SmtpSettingsAudit.ActionFor(before, after, passwordChange),
                    SmtpSettingsAudit.EntityType,
                    organizationId.Value.ToString(),
                    SmtpSettingsAudit.Snapshot(before),
                    SmtpSettingsAudit.Snapshot(after, passwordChange));
            }

            TempData["Message"] = after.IsEnabled
                ? "Company email settings saved. Emails for this company now go through " + after.Host + ":" + after.Port + "."
                : "Company email settings saved. Emails for this company use the platform default.";
            return RedirectToAction("Email");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SendTestEmail()
        {
            var organizationId = ResolveCurrentOrganizationId();
            if (!organizationId.HasValue)
            {
                TempData["Error"] = "Open Settings inside a company to send a test email.";
                return RedirectToAction("Index");
            }

            var recipient = ResolveCurrentUserEmail();
            if (string.IsNullOrWhiteSpace(recipient))
            {
                TempData["Error"] = "Your account has no email address to send the test to.";
                return RedirectToAction("Email");
            }

            var store = ResolveSmtpStore();
            var record = store == null ? null : store.Get(organizationId.Value);
            var settings = SmtpSettingsResolution.Resolve(record, null, ResolveSecretProtector(), false);
            if (settings == null)
            {
                TempData["Error"] = "Save the company SMTP host, port and From address first. "
                    + SmtpSettingsResolution.GetIncompleteReason(record, false);
                return RedirectToAction("Email");
            }

            var emailService = DependencyResolver.Current.GetService<IEmailService>();
            if (emailService == null)
            {
                TempData["Error"] = "Email service is unavailable.";
                return RedirectToAction("Email");
            }

            string errorMessage;
            var route = settings.DescribeRoute();
            if (emailService.ForSettings(settings).SendTestEmail(recipient, out errorMessage))
            {
                TempData["Message"] = "Test email sent to " + recipient + " through " + route + "."
                    + (record.IsEnabled ? string.Empty : " Company SMTP is still off, so other emails keep using the platform default until you turn it on.");
            }
            else
            {
                TempData["Error"] = "Test email through " + route + " failed: "
                    + (string.IsNullOrWhiteSpace(errorMessage) ? "unknown error." : errorMessage);
            }

            return RedirectToAction("Email");
        }

        private CompanySmtpSettingsViewModel BuildSmtpViewModel(int organizationId, CompanySmtpSettingsViewModel posted)
        {
            var store = ResolveSmtpStore();
            var available = store != null && store.IsAvailable();
            var record = available ? store.Get(organizationId) : null;
            var model = posted ?? new CompanySmtpSettingsViewModel();
            if (posted == null && record != null)
            {
                model.IsEnabled = record.IsEnabled;
                model.Host = record.Host;
                model.Port = record.Port;
                model.UseSsl = record.UseSsl;
                model.Username = record.Username;
                model.FromAddress = record.FromAddress;
                model.FromDisplayName = record.FromDisplayName;
            }

            // Never echo a password back, not even one that failed validation.
            model.NewPassword = null;
            model.ClearPassword = false;
            ModelState.Remove("NewPassword");
            ModelState.Remove("ClearPassword");

            var organization = UnitOfWork.Repository<Organization>().GetById(organizationId);
            model.OrganizationName = organization == null ? null : organization.Name;
            model.StoreAvailable = available;
            model.HasStoredPassword = record != null && record.HasPassword;
            model.UsesOwnSmtp = SmtpSettingsResolution.IsUsable(record);
            model.CanSendTest = SmtpSettingsResolution.GetIncompleteReason(record, false) == null;
            model.TestRecipient = ResolveCurrentUserEmail();
            if (!available)
            {
                model.CurrentRouteLabel = "Platform default";
                model.CurrentRouteDetail = "Company email settings are not available yet (database update 094 pending).";
            }
            else if (model.UsesOwnSmtp)
            {
                model.CurrentRouteLabel = "Own SMTP";
                model.CurrentRouteDetail = "Emails for this company go through " + record.Host + ":" + record.Port + ".";
            }
            else
            {
                model.CurrentRouteLabel = "Platform default";
                model.CurrentRouteDetail = SmtpSettingsResolution.GetIncompleteReason(record, true) + " Emails for this company use the platform default.";
            }

            if (record != null)
            {
                var stamp = record.UpdatedAt ?? record.CreatedAt;
                model.LastUpdatedLabel = KenyaTimeHelper.FormatUtc(stamp) + " " + KenyaTimeHelper.DisplayAbbreviation;
            }

            return model;
        }

        private string BuildSmtpRouteSummary(int? organizationId)
        {
            if (!organizationId.HasValue)
            {
                return null;
            }

            try
            {
                var store = ResolveSmtpStore();
                if (store == null || !store.IsAvailable())
                {
                    return "Platform default (company email settings not available yet).";
                }

                var record = store.Get(organizationId.Value);
                return SmtpSettingsResolution.IsUsable(record)
                    ? "Own SMTP: " + record.Host + ":" + record.Port
                    : "Platform default";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceWarning("SMTP status lookup failed: " + ex.Message);
                return null;
            }
        }

        private string ResolveCurrentUserEmail()
        {
            var profile = GetCurrentUserProfile();
            return profile == null ? null : profile.Email;
        }

        private static IOrganizationSmtpSettingsStore ResolveSmtpStore()
        {
            return DependencyResolver.Current.GetService<IOrganizationSmtpSettingsStore>();
        }

        private static ISecretProtector ResolveSecretProtector()
        {
            return DependencyResolver.Current.GetService<ISecretProtector>() ?? new EncryptionHelperSecretProtector();
        }

        private static string TrimOrNull(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
