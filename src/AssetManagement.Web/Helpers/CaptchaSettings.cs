using System;
using System.Configuration;

namespace AssetManagement.Web.Helpers
{
    public static class CaptchaSettings
    {
        public static bool IsLoginCaptchaEnabled()
        {
            var setting = ConfigurationManager.AppSettings["LoginCaptchaEnabled"];
            if (string.IsNullOrWhiteSpace(setting))
            {
                return true;
            }

            return !string.Equals(setting.Trim(), "false", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(setting.Trim(), "0", StringComparison.OrdinalIgnoreCase);
        }
    }
}
