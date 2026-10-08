using AssetManagement.Application.Email;

namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// Single place that decides which SMTP server an organization's email uses: its own settings when
    /// enabled and complete, otherwise <paramref name="platformDefault"/>. A null organization (platform /
    /// superadmin email) always gets the platform default.
    /// </summary>
    public interface ISmtpSettingsResolver
    {
        SmtpSettings Resolve(int? organizationId, SmtpSettings platformDefault);
    }
}
