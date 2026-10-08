using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Configuration;

namespace AssetManagement.Web.Helpers
{
    /// <summary>
    /// Encrypts temporary admin credential packages.
    /// v2 format: "v2:" + Base64(salt[16] | IV[16] | AES-256-CBC ciphertext | HMAC-SHA256[32]).
    /// KDF: PBKDF2-HMAC-SHA1 (Rfc2898DeriveBytes), 100000 iterations, random 16-byte salt;
    /// 64 derived bytes split as AES key (0..31) then HMAC key (32..63). Encrypt-then-MAC:
    /// HMAC covers salt || IV || ciphertext; compared in constant time.
    /// Legacy (non-v2) blobs use the historical static-salt PBKDF2 AES-CBC scheme and are
    /// decrypted with SystemEncryptionKeyPrevious first, then the current key.
    /// </summary>
    public static class EncryptionHelper
    {
        /// <summary>
        /// Historical committed default. Kept only so we can reject it as a live key.
        /// </summary>
        internal const string PublicDefaultKey = "AssetManagement-Secure-2026-Key-Default";

        private const string V2Prefix = "v2:";
        private const int SaltSize = 16;
        private const int IvSize = 16;
        private const int MacSize = 32;
        private const int DerivedKeyBytes = 64;
        private const int Pbkdf2Iterations = 100000;

        private static readonly byte[] LegacySalt = new byte[]
        {
            0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76
        };

        private static readonly object OverrideGate = new object();
        private static bool _overridesActive;
        private static string _overrideCurrentKey;
        private static string _overridePreviousKey;

        /// <summary>
        /// Test seam: supply current (and optional previous) keys without touching appSettings.
        /// </summary>
        internal static void SetKeysForTests(string currentKey, string previousKey)
        {
            lock (OverrideGate)
            {
                _overrideCurrentKey = currentKey;
                _overridePreviousKey = previousKey;
                _overridesActive = true;
            }
        }

        internal static void ClearKeysForTests()
        {
            lock (OverrideGate)
            {
                _overrideCurrentKey = null;
                _overridePreviousKey = null;
                _overridesActive = false;
            }
        }

        public static string Encrypt(string clearText)
        {
            if (string.IsNullOrEmpty(clearText))
            {
                return clearText;
            }

            return EncryptV2(clearText, ResolveCurrentKey());
        }

        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return cipherText;
            }

            if (cipherText.StartsWith(V2Prefix, StringComparison.Ordinal))
            {
                return DecryptV2(cipherText.Substring(V2Prefix.Length), ResolveCurrentKey());
            }

            return DecryptLegacyWithConfiguredKeys(cipherText);
        }


        private static string EncryptV2(string clearText, string secret)
        {
            var clearBytes = Encoding.UTF8.GetBytes(clearText);
            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] encKey;
            byte[] macKey;
            DeriveKeys(secret, salt, out encKey, out macKey);

            byte[] cipherBytes;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = encKey;
                aes.IV = iv;
                using (var encryptor = aes.CreateEncryptor())
                {
                    cipherBytes = encryptor.TransformFinalBlock(clearBytes, 0, clearBytes.Length);
                }
            }

            var macPayload = Concat(salt, iv, cipherBytes);
            byte[] mac;
            using (var hmac = new HMACSHA256(macKey))
            {
                mac = hmac.ComputeHash(macPayload);
            }

            ClearBytes(encKey);
            ClearBytes(macKey);

            var packed = Concat(salt, iv, cipherBytes, mac);
            return V2Prefix + Convert.ToBase64String(packed);
        }

        private static string DecryptV2(string base64Payload, string secret)
        {
            byte[] packed;
            try
            {
                packed = Convert.FromBase64String(base64Payload.Replace(" ", "+"));
            }
            catch (FormatException ex)
            {
                throw new CryptographicException("Credential package is not valid v2 ciphertext.", ex);
            }

            if (packed.Length < SaltSize + IvSize + MacSize + 1)
            {
                throw new CryptographicException("Credential package v2 payload is truncated.");
            }

            var salt = new byte[SaltSize];
            var iv = new byte[IvSize];
            var mac = new byte[MacSize];
            var cipherLen = packed.Length - SaltSize - IvSize - MacSize;
            var cipherBytes = new byte[cipherLen];

            Buffer.BlockCopy(packed, 0, salt, 0, SaltSize);
            Buffer.BlockCopy(packed, SaltSize, iv, 0, IvSize);
            Buffer.BlockCopy(packed, SaltSize + IvSize, cipherBytes, 0, cipherLen);
            Buffer.BlockCopy(packed, SaltSize + IvSize + cipherLen, mac, 0, MacSize);

            byte[] encKey;
            byte[] macKey;
            DeriveKeys(secret, salt, out encKey, out macKey);

            byte[] expectedMac;
            using (var hmac = new HMACSHA256(macKey))
            {
                expectedMac = hmac.ComputeHash(Concat(salt, iv, cipherBytes));
            }

            if (!FixedTimeEquals(mac, expectedMac))
            {
                ClearBytes(encKey);
                ClearBytes(macKey);
                throw new CryptographicException("Credential package MAC verification failed.");
            }

            try
            {
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    aes.Key = encKey;
                    aes.IV = iv;
                    using (var decryptor = aes.CreateDecryptor())
                    {
                        var clearBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
                        return Encoding.UTF8.GetString(clearBytes);
                    }
                }
            }
            catch (CryptographicException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new CryptographicException("Unable to decrypt credential package.", ex);
            }
            finally
            {
                ClearBytes(encKey);
                ClearBytes(macKey);
            }
        }

        private static string DecryptLegacyWithConfiguredKeys(string cipherText)
        {
            string previous;
            string current;
            ResolveKeys(out current, out previous);

            Exception lastError = null;

            if (!string.IsNullOrWhiteSpace(previous))
            {
                try
                {
                    return DecryptLegacy(cipherText, previous);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            try
            {
                return DecryptLegacy(cipherText, current);
            }
            catch (Exception ex)
            {
                lastError = ex;
            }

            throw new CryptographicException(
                "Unable to decrypt legacy credential package with the configured encryption keys.",
                lastError);
        }

        private static string DecryptLegacy(string cipherText, string key)
        {
            try
            {
                var normalized = cipherText.Replace(" ", "+");
                var cipherBytes = Convert.FromBase64String(normalized);
                using (var encryptor = Aes.Create())
                using (var pdb = new Rfc2898DeriveBytes(key, LegacySalt))
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
            catch (Exception ex)
            {
                throw new CryptographicException("Unable to decrypt legacy credential package.", ex);
            }
        }

        private static void DeriveKeys(string secret, byte[] salt, out byte[] encKey, out byte[] macKey)
        {
            using (var pdb = new Rfc2898DeriveBytes(secret, salt, Pbkdf2Iterations))
            {
                var derived = pdb.GetBytes(DerivedKeyBytes);
                encKey = new byte[32];
                macKey = new byte[32];
                Buffer.BlockCopy(derived, 0, encKey, 0, 32);
                Buffer.BlockCopy(derived, 32, macKey, 0, 32);
                ClearBytes(derived);
            }
        }

        private static string ResolveCurrentKey()
        {
            string current;
            string previous;
            ResolveKeys(out current, out previous);
            return current;
        }

        private static void ResolveKeys(out string current, out string previous)
        {
            lock (OverrideGate)
            {
                if (_overridesActive)
                {
                    current = _overrideCurrentKey;
                    previous = _overridePreviousKey;
                }
                else
                {
                    current = WebConfigurationManager.AppSettings["SystemEncryptionKey"];
                    previous = WebConfigurationManager.AppSettings["SystemEncryptionKeyPrevious"];
                }
            }

            ValidateKeyMaterial(current, "SystemEncryptionKey");
            if (!string.IsNullOrWhiteSpace(previous))
            {
                // Previous may equal the old public default (expected during rotation).
                if (previous.Trim().Length < 16)
                {
                    throw new InvalidOperationException(
                        "SystemEncryptionKeyPrevious is configured but is too short (minimum 16 characters).");
                }
            }
        }

        private static void ValidateKeyMaterial(string key, string settingName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    settingName + " is missing. Set a unique secret in appSettings (for example via the external secrets file) and remove any reliance on the public default.");
            }

            if (string.Equals(key.Trim(), PublicDefaultKey, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    settingName + " still equals the public default value. Rotate to a unique secret before encrypting or decrypting credential packages.");
            }

            if (key.Trim().Length < 16)
            {
                throw new InvalidOperationException(
                    settingName + " is too short (minimum 16 characters).");
            }
        }

        private static byte[] Concat(params byte[][] parts)
        {
            var total = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                total += parts[i].Length;
            }

            var result = new byte[total];
            var offset = 0;
            for (var i = 0; i < parts.Length; i++)
            {
                Buffer.BlockCopy(parts[i], 0, result, offset, parts[i].Length);
                offset += parts[i].Length;
            }

            return result;
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }

            return diff == 0;
        }

        private static void ClearBytes(byte[] buffer)
        {
            if (buffer != null)
            {
                Array.Clear(buffer, 0, buffer.Length);
            }
        }
    }
}