using System;

namespace AssetManagement.Domain.Entities
{
    /// <summary>
    /// One-time encrypted credential package (e.g. new organization admin login details).
    /// </summary>
    public class TemporaryCredential
    {
        public int Id { get; set; }

        public string Token { get; set; }

        public string EncryptedData { get; set; }

        public DateTime ExpiryDate { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedDate { get; set; }

        public string CredentialType { get; set; }
    }
}
