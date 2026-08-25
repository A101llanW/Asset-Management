using System;
using System.Configuration;

namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Production deployment toggles (Web.config / Release transform).
    /// </summary>
    public static class DeploymentSecuritySettings
    {
        public static bool RequireMfaForAllUsers
        {
            get { return ReadBooleanAppSetting("RequireMfaForAllUsers", defaultValue: false); }
        }

        public static bool RequireHttpsRedirect
        {
            get { return ReadBooleanAppSetting("RequireHttpsRedirect", defaultValue: false); }
        }

        public static bool RequireSecureCookies
        {
            get { return ReadBooleanAppSetting("RequireSecureCookies", defaultValue: false); }
        }

        /// <summary>
        /// When false (Release/production), MFA codes must be emailed and cannot be bypassed with any code.
        /// </summary>
        public static bool MfaAllowAnyCode
        {
            get { return ReadBooleanAppSetting("MfaAllowAnyCode", defaultValue: false); }
        }

        /// <summary>
        /// True when MFA/password-reset flows must deliver email (production Release builds).
        /// </summary>
        public static bool RequiresSmtpForAuthEmails
        {
            get { return !MfaAllowAnyCode; }
        }

        private static bool ReadBooleanAppSetting(string key, bool defaultValue)
        {
            var setting = ConfigurationManager.AppSettings[key];
            if (string.IsNullOrWhiteSpace(setting))
            {
                return defaultValue;
            }

            return string.Equals(setting.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(setting.Trim(), "1", StringComparison.OrdinalIgnoreCase);
        }
    }
}
