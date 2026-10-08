using System;

namespace AssetManagement.Application.Email
{
    /// <summary>
    /// One row of OrganizationSmtpSettings (migration 094). PasswordEncrypted is EncryptionHelper v2
    /// ciphertext; the clear password is never stored, shown or logged.
    /// </summary>
    public class OrganizationSmtpSettingsRecord
    {
        public int Id { get; set; }

        public int OrganizationId { get; set; }

        public string Host { get; set; }

        public int Port { get; set; }

        public bool UseSsl { get; set; }

        public string Username { get; set; }

        public string PasswordEncrypted { get; set; }

        public string FromAddress { get; set; }

        public string FromDisplayName { get; set; }

        public bool IsEnabled { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        public string CreatedByUserId { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string UpdatedByUserId { get; set; }

        public bool HasPassword
        {
            get { return !string.IsNullOrEmpty(PasswordEncrypted); }
        }

        public OrganizationSmtpSettingsRecord Clone()
        {
            return (OrganizationSmtpSettingsRecord)MemberwiseClone();
        }
    }
}
