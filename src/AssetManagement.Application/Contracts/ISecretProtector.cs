namespace AssetManagement.Application.Contracts
{
    /// <summary>
    /// Encrypts small secrets at rest (the web app implements it with EncryptionHelper v2).
    /// </summary>
    public interface ISecretProtector
    {
        string Protect(string clearText);

        string Unprotect(string protectedText);
    }
}
