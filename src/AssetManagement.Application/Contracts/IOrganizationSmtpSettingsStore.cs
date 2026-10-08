using AssetManagement.Application.Email;

namespace AssetManagement.Application.Contracts
{
    public interface IOrganizationSmtpSettingsStore
    {
        /// <summary>False until migration 094 (OrganizationSmtpSettings) is applied.</summary>
        bool IsAvailable();

        /// <summary>The organization's row, or null when none is saved or the table is missing.</summary>
        OrganizationSmtpSettingsRecord Get(int organizationId);

        /// <summary>Insert or update the organization's row (UTC audit columns). Throws when the table is missing.</summary>
        void Save(OrganizationSmtpSettingsRecord record, string actorUserId);
    }
}
