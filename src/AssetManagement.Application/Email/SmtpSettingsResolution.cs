using System;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Application.Email
{
    /// <summary>
    /// Pure rules for choosing an email's SMTP server: the organization's own settings when they are
    /// active, enabled and complete, otherwise the platform default. No I/O, so it is unit tested directly.
    /// </summary>
    public static class SmtpSettingsResolution
    {
        public const string DefaultFromName = "Asset Management Module";

        /// <summary>Null when the record can be used; otherwise a short, user-facing reason (no secrets).</summary>
        public static string GetIncompleteReason(OrganizationSmtpSettingsRecord record, bool requireEnabled)
        {
            if (record == null)
            {
                return "No company SMTP settings are saved.";
            }

            if (!record.IsActive)
            {
                return "Company SMTP settings were removed.";
            }

            if (requireEnabled && !record.IsEnabled)
            {
                return "Company SMTP is turned off.";
            }

            if (string.IsNullOrWhiteSpace(record.Host))
            {
                return "Company SMTP host is missing.";
            }

            if (record.Port < 1 || record.Port > 65535)
            {
                return "Company SMTP port must be between 1 and 65535.";
            }

            if (string.IsNullOrWhiteSpace(record.FromAddress))
            {
                return "Company SMTP From address is missing.";
            }

            if (!IsValidEmailAddress(record.FromAddress))
            {
                return "Company SMTP From address is not a valid email address.";
            }

            if (!string.IsNullOrWhiteSpace(record.Username) && !record.HasPassword)
            {
                return "Company SMTP username is set but no password is saved.";
            }

            return null;
        }

        public static bool IsUsable(OrganizationSmtpSettingsRecord record)
        {
            return GetIncompleteReason(record, true) == null;
        }

        /// <summary>
        /// Effective settings for one organization. Falls back to <paramref name="platformDefault"/> (which may be
        /// null when the caller only wants the company settings) with <see cref="SmtpSettings.Reason"/> explaining why.
        /// </summary>
        public static SmtpSettings Resolve(
            OrganizationSmtpSettingsRecord record,
            SmtpSettings platformDefault,
            ISecretProtector protector,
            bool requireEnabled)
        {
            var reason = GetIncompleteReason(record, requireEnabled);
            if (reason == null && record.HasPassword && protector == null)
            {
                reason = "Company SMTP password cannot be read on this server.";
            }

            if (reason != null)
            {
                return platformDefault == null ? null : platformDefault.WithReason(reason);
            }

            var encrypted = record.PasswordEncrypted;
            Func<string> passwordProvider;
            if (record.HasPassword)
            {
                passwordProvider = () => protector.Unprotect(encrypted);
            }
            else
            {
                passwordProvider = () => null;
            }

            var fromName = !string.IsNullOrWhiteSpace(record.FromDisplayName)
                ? record.FromDisplayName.Trim()
                : (platformDefault != null && !string.IsNullOrWhiteSpace(platformDefault.FromName) ? platformDefault.FromName : DefaultFromName);

            return new SmtpSettings(
                SmtpSettingsSource.Organization,
                record.OrganizationId,
                record.Host.Trim(),
                record.Port,
                record.UseSsl,
                string.IsNullOrWhiteSpace(record.Username) ? null : record.Username.Trim(),
                passwordProvider,
                record.FromAddress.Trim(),
                fromName,
                true,
                record.IsEnabled ? "Company SMTP is on." : "Company SMTP settings (not turned on yet).");
        }

        public static bool IsValidEmailAddress(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf('@') <= 0)
            {
                return false;
            }

            try
            {
                var parsed = new System.Net.Mail.MailAddress(value.Trim());
                return string.Equals(parsed.Address, value.Trim(), StringComparison.OrdinalIgnoreCase);
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
