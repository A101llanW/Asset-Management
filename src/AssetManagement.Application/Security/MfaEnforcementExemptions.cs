using System;

namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Controllers/actions that must not redirect into forced MFA enrollment.
    /// </summary>
    public static class MfaEnforcementExemptions
    {
        public static bool IsExemptFromForcedSetup(string controllerName, string actionName)
        {
            if (string.IsNullOrWhiteSpace(controllerName))
            {
                return false;
            }

            if (!string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(controllerName, "Captcha", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(controllerName, "Home", StringComparison.OrdinalIgnoreCase);
            }

            if (string.IsNullOrWhiteSpace(actionName))
            {
                return false;
            }

            return string.Equals(actionName, "SetupMfa", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "VerifyMfa", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "SendSetupMfaCode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ResendMfaCode", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "LogOff", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "Login", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "VerifyEmail", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actionName, "ChangePassword", StringComparison.OrdinalIgnoreCase);
        }
    }
}
