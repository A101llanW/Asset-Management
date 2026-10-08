using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class EncryptionHelperTests
    {
        private const string CurrentKey = "unit-test-current-key-0123456789abcdef0123456789";
        private const string OtherKey = "unit-test-other-key-fedcba9876543210fedcba987654";
        private const string Payload = "{\"CompanyName\":\"Throwaway Org\",\"AdminUsername\":\"admin@example.test\",\"AdminPassword\":\"Temp#Pass-1\"}";

        [TearDown]
        public void TearDown()
        {
            EncryptionHelper.ClearKeysForTests();
        }

        [Test]
        public void Encrypt_V2_RoundTripsAndUsesV2Prefix()
        {
            EncryptionHelper.SetKeysForTests(CurrentKey, null);

            var cipher = EncryptionHelper.Encrypt(Payload);

            Assert.That(cipher.StartsWith("v2:", StringComparison.Ordinal), Is.True);
            Assert.That(cipher.Contains("Temp#Pass-1"), Is.False);
            Assert.AreEqual(Payload, EncryptionHelper.Decrypt(cipher));
        }

        [Test]
        public void Encrypt_V2_LayoutIsSaltIvCiphertextMac()
        {
            EncryptionHelper.SetKeysForTests(CurrentKey, null);

            var packed = Convert.FromBase64String(EncryptionHelper.Encrypt("x").Substring(3));

            // 16 salt + 16 IV + one AES block (PKCS7) + 32 HMAC-SHA256
            Assert.AreEqual(16 + 16 + 16 + 32, packed.Length);
        }

        [Test]
        public void Encrypt_V2_UsesFreshSaltAndIvEachTime()
        {
            EncryptionHelper.SetKeysForTests(CurrentKey, null);

            var first = EncryptionHelper.Encrypt(Payload);
            var second = EncryptionHelper.Encrypt(Payload);

            Assert.AreNotEqual(first, second);
            Assert.AreEqual(Payload, EncryptionHelper.Decrypt(first));
            Assert.AreEqual(Payload, EncryptionHelper.Decrypt(second));
        }

        [Test]
        public void Decrypt_V2_DoesNotFallBackToPreviousKey()
        {
            EncryptionHelper.SetKeysForTests(OtherKey, null);
            var cipher = EncryptionHelper.Encrypt(Payload);

            EncryptionHelper.SetKeysForTests(CurrentKey, OtherKey);

            Assert.Throws<CryptographicException>(() => EncryptionHelper.Decrypt(cipher));
        }

        [Test]
        public void Decrypt_Legacy_UsesPreviousKey_PublicDefault()
        {
            var legacy = LegacyEncrypt(Payload, EncryptionHelper.PublicDefaultKey);
            EncryptionHelper.SetKeysForTests(CurrentKey, EncryptionHelper.PublicDefaultKey);

            Assert.AreEqual(Payload, EncryptionHelper.Decrypt(legacy));
        }

        [Test]
        public void Decrypt_Legacy_ToleratesSpacesForPlusSigns()
        {
            var legacy = LegacyEncrypt(Payload, EncryptionHelper.PublicDefaultKey).Replace("+", " ");
            EncryptionHelper.SetKeysForTests(CurrentKey, EncryptionHelper.PublicDefaultKey);

            Assert.AreEqual(Payload, EncryptionHelper.Decrypt(legacy));
        }

        [Test]
        public void Decrypt_Legacy_FallsBackToCurrentKey()
        {
            var legacy = LegacyEncrypt(Payload, CurrentKey);
            EncryptionHelper.SetKeysForTests(CurrentKey, EncryptionHelper.PublicDefaultKey);

            Assert.AreEqual(Payload, EncryptionHelper.Decrypt(legacy));
        }

        [Test]
        public void Decrypt_Legacy_ThrowsWhenNoConfiguredKeyMatches_InsteadOfReturningCiphertext()
        {
            var legacy = LegacyEncrypt(Payload, EncryptionHelper.PublicDefaultKey);
            EncryptionHelper.SetKeysForTests(CurrentKey, null);

            Assert.Throws<CryptographicException>(() => EncryptionHelper.Decrypt(legacy));
        }

        [Test]
        public void Decrypt_Legacy_GarbageThrows()
        {
            EncryptionHelper.SetKeysForTests(CurrentKey, EncryptionHelper.PublicDefaultKey);

            Assert.Throws<CryptographicException>(() => EncryptionHelper.Decrypt("not base64 at all!!"));
        }

        [TestCase(0, TestName = "Decrypt_V2_TamperedSalt_FailsClosed")]
        [TestCase(20, TestName = "Decrypt_V2_TamperedIv_FailsClosed")]
        [TestCase(40, TestName = "Decrypt_V2_TamperedCiphertext_FailsClosed")]
        [TestCase(-1, TestName = "Decrypt_V2_TamperedMac_FailsClosed")]
        public void Decrypt_V2_TamperFailsClosed(int index)
        {
            EncryptionHelper.SetKeysForTests(CurrentKey, null);
            var packed = Convert.FromBase64String(EncryptionHelper.Encrypt(Payload).Substring(3));
            var at = index < 0 ? packed.Length - 1 : index;
            packed[at] ^= 0x01;
            var tampered = "v2:" + Convert.ToBase64String(packed);

            var ex = Assert.Throws<CryptographicException>(() => EncryptionHelper.Decrypt(tampered));
            Assert.That(ex.Message, Is.StringContaining("MAC"));
        }

        [Test]
        public void Decrypt_V2_TruncatedPayloadFails()
        {
            EncryptionHelper.SetKeysForTests(CurrentKey, null);
            var tooShort = "v2:" + Convert.ToBase64String(new byte[40]);

            Assert.Throws<CryptographicException>(() => EncryptionHelper.Decrypt(tooShort));
        }

        [Test]
        public void Decrypt_V2_WrongCurrentKeyFails()
        {
            EncryptionHelper.SetKeysForTests(OtherKey, null);
            var cipher = EncryptionHelper.Encrypt(Payload);
            EncryptionHelper.SetKeysForTests(CurrentKey, null);

            Assert.Throws<CryptographicException>(() => EncryptionHelper.Decrypt(cipher));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void Encrypt_MissingKey_ThrowsDescriptiveError(string key)
        {
            EncryptionHelper.SetKeysForTests(key, null);

            var ex = Assert.Throws<InvalidOperationException>(() => EncryptionHelper.Encrypt(Payload));
            Assert.That(ex.Message, Is.StringContaining("SystemEncryptionKey is missing"));
        }

        [Test]
        public void Encrypt_PublicDefaultKey_ThrowsDescriptiveError()
        {
            EncryptionHelper.SetKeysForTests(EncryptionHelper.PublicDefaultKey, null);

            var ex = Assert.Throws<InvalidOperationException>(() => EncryptionHelper.Encrypt(Payload));
            Assert.That(ex.Message, Is.StringContaining("public default"));
        }

        [Test]
        public void Decrypt_PublicDefaultAsCurrentKey_ThrowsEvenForLegacy()
        {
            var legacy = LegacyEncrypt(Payload, EncryptionHelper.PublicDefaultKey);
            EncryptionHelper.SetKeysForTests(" " + EncryptionHelper.PublicDefaultKey + " ", null);

            Assert.Throws<InvalidOperationException>(() => EncryptionHelper.Decrypt(legacy));
        }

        [Test]
        public void Encrypt_WithoutAppSetting_NoLongerFallsBackToDefault()
        {
            // The test host has no SystemEncryptionKey appSetting: the old code silently used the public default.
            EncryptionHelper.ClearKeysForTests();

            Assert.Throws<InvalidOperationException>(() => EncryptionHelper.Encrypt(Payload));
        }

        [Test]
        public void EmptyInputs_PassThrough()
        {
            EncryptionHelper.SetKeysForTests(CurrentKey, null);

            Assert.IsNull(EncryptionHelper.Encrypt(null));
            Assert.AreEqual(string.Empty, EncryptionHelper.Decrypt(string.Empty));
        }

        /// <summary>Independent copy of the pre-v2 EncryptionHelper.Encrypt (static salt, key-derived IV).</summary>
        private static string LegacyEncrypt(string clearText, string key)
        {
            var salt = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };
            var clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (var encryptor = Aes.Create())
            using (var pdb = new Rfc2898DeriveBytes(key, salt))
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
    }
}