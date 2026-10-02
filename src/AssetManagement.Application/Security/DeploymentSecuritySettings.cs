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
        /// Explicit demo / security-relaxed chrome switch. When unset, falls back to MfaAllowAnyCode.
        /// </summary>
        public static bool ShowDemoSecurityBanner
        {
            get
            {
                var setting = ConfigurationManager.AppSettings["ShowDemoSecurityBanner"];
                if (string.IsNullOrWhiteSpace(setting))
                {
                    setting = ConfigurationManager.AppSettings["DemoMode"];
                }

                if (string.IsNullOrWhiteSpace(setting))
                {
                    return MfaAllowAnyCode;
                }

                return string.Equals(setting.Trim(), "true", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(setting.Trim(), "1", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>
        /// True when MFA any-code bypass is on or an explicit demo banner flag is set.
        /// </summary>
        public static bool IsSecurityRelaxed
        {
            get { return MfaAllowAnyCode || ShowDemoSecurityBanner; }
        }

        /// <summary>
        /// Whether authenticated shell should show the DEMO / SECURITY RELAXED banner.
        /// </summary>
        public static bool ShowDemoBanner
        {
            get { return ShowDemoSecurityBanner; }
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