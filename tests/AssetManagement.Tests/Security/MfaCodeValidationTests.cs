using System;
using AssetManagement.Infrastructure.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class MfaCodeValidationTests
    {
        private static readonly DateTime FixedUtcNow = new DateTime(2026, 8, 5, 12, 0, 0, DateTimeKind.Utc);

        [Test]
        public void Validate_RejectsArbitraryCode_WhenBypassDisabled()
        {
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: false,
                storedCode: "123456",
                expiryUtc: FixedUtcNow.AddMinutes(5),
                submittedCode: "999999",
                utcNow: FixedUtcNow);

            Assert.IsFalse(accepted);
        }

        [Test]
        public void Validate_AcceptsMatchingCode_WhenBypassDisabled()
        {
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: false,
                storedCode: "123456",
                expiryUtc: FixedUtcNow.AddMinutes(5),
                submittedCode: "123456",
                utcNow: FixedUtcNow);

            Assert.IsTrue(accepted);
        }

        [Test]
        public void Validate_RejectsExpiredCode_WhenBypassDisabled()
        {
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: false,
                storedCode: "123456",
                expiryUtc: FixedUtcNow.AddMinutes(-1),
                submittedCode: "123456",
                utcNow: FixedUtcNow);

            Assert.IsFalse(accepted);
        }

        [Test]
        public void Validate_RejectsEmptyCode_EvenWhenBypassEnabled()
        {
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: true,
                storedCode: "123456",
                expiryUtc: FixedUtcNow.AddMinutes(5),
                submittedCode: "   ",
                utcNow: FixedUtcNow);

            Assert.IsFalse(accepted);
        }

        [Test]
        public void Validate_AcceptsAnyNonEmptyCode_WhenBypassEnabled()
        {
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: true,
                storedCode: null,
                expiryUtc: null,
                submittedCode: "000000",
                utcNow: FixedUtcNow);

            Assert.IsTrue(accepted);
        }
    }
}
