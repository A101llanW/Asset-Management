using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Configuration;

namespace AssetManagement.Web.Helpers
{
    public static class EncryptionHelper
    {
        private static readonly string EncryptionKey =
            WebConfigurationManager.AppSettings["SystemEncryptionKey"] ?? "AssetManagement-Secure-2026-Key-Default";

        private static readonly byte[] Salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        public static string Encrypt(string clearText)
        {
            if (string.IsNullOrEmpty(clearText))
            {
                return clearText;
            }

            var clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (var encryptor = Aes.Create())
            using (var pdb = new Rfc2898DeriveBytes(EncryptionKey, Salt))
            {
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                    }

                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return cipherText;
            }

            try
            {
                cipherText = cipherText.Replace(" ", "+");
                var cipherBytes = Convert.FromBase64String(cipherText);
                using (var encryptor = Aes.Create())
                using (var pdb = new Rfc2898DeriveBytes(EncryptionKey, Salt))
                {
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (var ms = new MemoryStream())
                    {
                        using (var cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                        }

                        return Encoding.Unicode.GetString(ms.ToArray());
                    }
                }
            }
            catch (Exception)
            {
                return cipherText;
            }
        }
    }
}
