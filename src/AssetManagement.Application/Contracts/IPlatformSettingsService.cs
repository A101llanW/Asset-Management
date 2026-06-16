namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// Platform-level settings (OrganizationId IS NULL) with Web.config fallback.
    /// Web.config keys: SmtpHost, SmtpPort, SmtpUser, SmtpPassword, SmtpEnableSsl, FromEmail, FromName, ExternalBaseUrl
    /// </summary>
    public interface IPlatformSettingsService
    {
        string GetSetting(string key, string defaultValue);

        void SetSetting(string key, string value, string description);

        string GetExternalBaseUrl();
    }
}
