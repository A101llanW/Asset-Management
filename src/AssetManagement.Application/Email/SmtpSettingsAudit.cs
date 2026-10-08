using System.Globalization;

namespace AssetManagement.Application.Email
{
    /// <summary>
    /// Audit text for company SMTP changes. The password is reported only as set / none and
    /// changed / cleared / unchanged; its value (clear or encrypted) never reaches the audit log.
    /// </summary>
    public static class SmtpSettingsAudit
    {
        public const string EntityType = "OrganizationSmtpSettings";
        public const string ActionCreate = "Settings.Smtp.Create";
        public const string ActionUpdate = "Settings.Smtp.Update";
        public const string ActionEnable = "Settings.Smtp.Enable";
        public const string ActionDisable = "Settings.Smtp.Disable";
        public const string ActionClearPassword = "Settings.Smtp.ClearPassword";

        public const string PasswordChanged = "changed";
        public const string PasswordCleared = "cleared";
        public const string PasswordUnchanged = "unchanged";

        public static string ActionFor(OrganizationSmtpSettingsRecord before, OrganizationSmtpSettingsRecord after, string passwordChange)
        {
            if (before == null || !before.IsActive)
            {
                return ActionCreate;
            }

            if (after != null && before.IsEnabled != after.IsEnabled)
            {
                return after.IsEnabled ? ActionEnable : ActionDisable;
            }

            if (passwordChange == PasswordCleared && SameServerFields(before, after))
            {
                return ActionClearPassword;
            }

            return ActionUpdate;
        }

        public static string Snapshot(OrganizationSmtpSettingsRecord record)
        {
            return Snapshot(record, null);
        }

        public static string Snapshot(OrganizationSmtpSettingsRecord record, string passwordChange)
        {
            if (record == null)
            {
                return null;
            }

            var text = string.Format(
                CultureInfo.InvariantCulture,
                "Enabled={0}; Host={1}; Port={2}; UseSsl={3}; Username={4}; From={5}; FromName={6}; Password={7}",
                record.IsEnabled,
                record.Host ?? string.Empty,
                record.Port,
                record.UseSsl,
                record.Username ?? string.Empty,
                record.FromAddress ?? string.Empty,
                record.FromDisplayName ?? string.Empty,
                record.HasPassword ? "(set)" : "(none)");
            if (!string.IsNullOrEmpty(passwordChange))
            {
                text += "; PasswordChange=" + passwordChange;
            }

            return text;
        }

        private static bool SameServerFields(OrganizationSmtpSettingsRecord a, OrganizationSmtpSettingsRecord b)
        {
            if (a == null || b == null)
            {
                return false;
            }

            return string.Equals(a.Host ?? string.Empty, b.Host ?? string.Empty)
                && a.Port == b.Port
                && a.UseSsl == b.UseSsl
                && string.Equals(a.Username ?? string.Empty, b.Username ?? string.Empty)
                && string.Equals(a.FromAddress ?? string.Empty, b.FromAddress ?? string.Empty)
                && string.Equals(a.FromDisplayName ?? string.Empty, b.FromDisplayName ?? string.Empty);
        }
    }
}
