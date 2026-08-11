using System;
using System.Collections.Generic;

namespace AssetManagement.Application.Security
{
    /// <summary>
    /// Describes whether outbound SMTP is ready for MFA and password-reset delivery.
    /// </summary>
    public sealed class EmailConfigurationStatus
    {
        public bool HasSmtpHost { get; set; }

        public bool HasFromEmail { get; set; }

        public bool HasExternalBaseUrl { get; set; }

        public bool RequiresDelivery { get; set; }

        public bool IsReadyForAuthDelivery
        {
            get { return HasSmtpHost && HasFromEmail; }
        }

        public bool IsBlockingProductionAuth
        {
            get { return RequiresDelivery && !IsReadyForAuthDelivery; }
        }

        public string[] GetMissingRequirements()
        {
            var missing = new List<string>();
            if (!HasSmtpHost)
            {
                missing.Add("SmtpHost");
            }

            if (!HasFromEmail)
            {
                missing.Add("FromEmail");
            }

            return missing.ToArray();
        }

        public string GetSummary()
        {
            if (!RequiresDelivery)
            {
                return IsReadyForAuthDelivery
                    ? "SMTP configured. MFA dev bypass is enabled, so codes may also appear in trace output."
                    : "SMTP is optional while MfaAllowAnyCode=true (development/E2E).";
            }

            if (IsReadyForAuthDelivery)
            {
                return HasExternalBaseUrl
                    ? "SMTP is configured for MFA and password reset."
                    : "SMTP is configured for MFA. Set ExternalBaseUrl for reliable password reset links.";
            }

            return "SMTP is required for MFA and password reset when MfaAllowAnyCode=false. Missing: "
                + string.Join(", ", GetMissingRequirements())
                + ".";
        }
    }
}
