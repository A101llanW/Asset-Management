using AssetManagement.Application.Contracts;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// <see cref="ISecretProtector"/> over <see cref="EncryptionHelper"/> (v2 encrypt-then-MAC with the
    /// rotated SystemEncryptionKey). Used for company SMTP passwords.
    /// </summary>
    public sealed class EncryptionHelperSecretProtector : ISecretProtector
    {
        public string Protect(string clearText)
        {
            return EncryptionHelper.Encrypt(clearText);
        }

        public string Unprotect(string protectedText)
        {
            return EncryptionHelper.Decrypt(protectedText);
        }
    }
}
