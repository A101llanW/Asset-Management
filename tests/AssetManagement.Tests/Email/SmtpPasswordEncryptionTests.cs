using System;
using AssetManagement.Application.Email;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Email
{
    [TestFixture]
    public class SmtpPasswordEncryptionTests
    {
        private const string Key = "unit-test-smtp-key-0123456789abcdef0123456789abcd";
        private const string Dummy = "dummy-not-a-real-password";

        [SetUp]
        public void SetUp()
        {
            EncryptionHelper.SetKeysForTests(Key, null);
        }

        [TearDown]
        public void TearDown()
        {
            EncryptionHelper.ClearKeysForTests();
        }

        [Test]
        public void Protector_RoundTrips_WithV2Format_AndRandomSalt()
        {
            var protector = new EncryptionHelperSecretProtector();
            var first = protector.Protect(Dummy);
            var second = protector.Protect(Dummy);

            Assert.That(first.StartsWith("v2:", StringComparison.Ordinal), Is.True);
            Assert.That(first.Contains(Dummy), Is.False);
            Assert.That(first, Is.Not.EqualTo(second), "random salt/IV per encryption");
            Assert.That(protector.Unprotect(first), Is.EqualTo(Dummy));
            Assert.That(protector.Unprotect(second), Is.EqualTo(Dummy));
        }

        [Test]
        public void TamperedCiphertext_IsRejected()
        {
            var protector = new EncryptionHelperSecretProtector();
            var cipher = protector.Protect(Dummy);
            var bytes = Convert.FromBase64String(cipher.Substring(3));
            bytes[40] ^= 0x01;
            var tampered = "v2:" + Convert.ToBase64String(bytes);
            Assert.That(() => protector.Unprotect(tampered), Throws.InstanceOf<Exception>());
        }

        [Test]
        public void StoredCiphertext_ResolvesToClearPassword_ButNeverReachesAudit()
        {
            var protector = new EncryptionHelperSecretProtector();
            var record = new OrganizationSmtpSettingsRecord
            {
                OrganizationId = 13,
                Host = "127.0.0.1",
                Port = 2525,
                Username = "sinkuser",
                PasswordEncrypted = protector.Protect(Dummy),
                FromAddress = "testwf@example.test",
                IsEnabled = true,
                IsActive = true
            };

            var settings = SmtpSettingsResolution.Resolve(record, null, protector, true);
            Assert.That(settings.GetPassword(), Is.EqualTo(Dummy));

            var audit = SmtpSettingsAudit.Snapshot(record, SmtpSettingsAudit.PasswordChanged);
            Assert.That(audit.Contains(Dummy), Is.False);
            Assert.That(audit.Contains(record.PasswordEncrypted), Is.False);
            Assert.That(audit.Contains(record.PasswordEncrypted.Substring(3, 12)), Is.False);
        }
    }
}
