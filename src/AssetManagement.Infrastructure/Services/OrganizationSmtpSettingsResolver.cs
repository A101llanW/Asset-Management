using AssetManagement.Application.Contracts;
using AssetManagement.Application.Email;

namespace AssetManagement.Infrastructure.Services
{
    /// <summary>
    /// Reads the organization's OrganizationSmtpSettings row and applies <see cref="SmtpSettingsResolution"/>.
    /// One indexed lookup per email; the company password is only decrypted when a message is sent.
    /// </summary>
    public class OrganizationSmtpSettingsResolver : ISmtpSettingsResolver
    {
        private readonly IOrganizationSmtpSettingsStore _store;
        private readonly ISecretProtector _protector;

        public OrganizationSmtpSettingsResolver(IOrganizationSmtpSettingsStore store, ISecretProtector protector)
        {
            _store = store;
            _protector = protector;
        }

        public SmtpSettings Resolve(int? organizationId, SmtpSettings platformDefault)
        {
            if (!organizationId.HasValue || organizationId.Value <= 0 || _store == null)
            {
                return platformDefault;
            }

            var record = _store.Get(organizationId.Value);
            return SmtpSettingsResolution.Resolve(record, platformDefault, _protector, true);
        }
    }
}
