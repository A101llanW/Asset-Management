using AssetManagement.Infrastructure.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class PasswordHasherTests
    {
        private const string DemoPassword = "P@ssw0rd!";

        [Test]
        public void HashPassword_UsesCurrentVersionByte()
        {
            var hash = PasswordHasher.HashPassword(DemoPassword);
            var decoded = System.Convert.FromBase64String(hash);

            Assert.AreEqual(PasswordHasher.VersionCurrent, decoded[0]);
        }

        [Test]
        public void VerifyHashedPassword_LegacySeedHash_AcceptsDemoPasswordAndRequiresRehash()
        {
            var result = PasswordHasher.VerifyHashedPassword(PasswordHasher.LegacySeedHashBase64, DemoPassword);

            Assert.AreEqual(PasswordVerificationResult.SuccessRehashNeeded, result);
        }

        [Test]
        public void VerifyHashedPassword_LegacySeedHash_RejectsWrongPassword()
        {
            var result = PasswordHasher.VerifyHashedPassword(PasswordHasher.LegacySeedHashBase64, "WrongPassword1!");

            Assert.AreEqual(PasswordVerificationResult.Failed, result);
        }

        [Test]
        public void VerifyHashedPassword_CurrentHash_AcceptsPasswordWithoutRehash()
        {
            var hash = PasswordHasher.HashPassword(DemoPassword);
            var result = PasswordHasher.VerifyHashedPassword(hash, DemoPassword);

            Assert.AreEqual(PasswordVerificationResult.Success, result);
        }

        [Test]
        public void HashPassword_UpgradesFromLegacyFormat()
        {
            var legacyHash = PasswordHasher.LegacySeedHashBase64;
            var upgradedHash = PasswordHasher.HashPassword(DemoPassword);

            Assert.AreNotEqual(legacyHash, upgradedHash);
            Assert.AreEqual(PasswordVerificationResult.Success, PasswordHasher.VerifyHashedPassword(upgradedHash, DemoPassword));
        }

        [Test]
        public void VerifyHashedPassword_RejectsUnknownVersionByte()
        {
            var currentHash = PasswordHasher.HashPassword(DemoPassword);
            var decoded = System.Convert.FromBase64String(currentHash);
            decoded[0] = 99;
            var tamperedHash = System.Convert.ToBase64String(decoded);

            var result = PasswordHasher.VerifyHashedPassword(tamperedHash, DemoPassword);

            Assert.AreEqual(PasswordVerificationResult.Failed, result);
        }

        [Test]
        public void VerifyHashedPassword_RejectsMalformedPayload()
        {
            var result = PasswordHasher.VerifyHashedPassword("not-valid-base64!!!", DemoPassword);

            Assert.AreEqual(PasswordVerificationResult.Failed, result);
        }
    }
}
