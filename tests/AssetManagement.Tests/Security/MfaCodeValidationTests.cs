using System;
using AssetManagement.Application.Security;
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

        [Test]
        public void Validate_RejectsNullSubmittedCode()
        {
            Assert.IsFalse(MfaCodeValidator.Validate(false, "123456", FixedUtcNow.AddMinutes(5), null, FixedUtcNow));
            Assert.IsFalse(MfaCodeValidator.Validate(true, "123456", FixedUtcNow.AddMinutes(5), null, FixedUtcNow));
        }

        [Test]
        public void Validate_RejectsEmptyStringSubmittedCode()
        {
            Assert.IsFalse(MfaCodeValidator.Validate(false, "123456", FixedUtcNow.AddMinutes(5), string.Empty, FixedUtcNow));
            Assert.IsFalse(MfaCodeValidator.Validate(true, null, null, string.Empty, FixedUtcNow));
        }

        [Test]
        public void Validate_TrimsStoredAndSubmitted_WhenComparing()
        {
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: false,
                storedCode: " 123456 ",
                expiryUtc: FixedUtcNow.AddMinutes(5),
                submittedCode: "123456",
                utcNow: FixedUtcNow);

            Assert.IsTrue(accepted);
        }

        [Test]
        public void Validate_IsOrdinalCaseSensitive()
        {
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: false,
                storedCode: "AbC123",
                expiryUtc: FixedUtcNow.AddMinutes(5),
                submittedCode: "abc123",
                utcNow: FixedUtcNow);

            Assert.IsFalse(accepted);
        }

        [Test]
        public void Validate_RejectsWhenStoredCodeMissing_AndBypassDisabled()
        {
            Assert.IsFalse(MfaCodeValidator.Validate(false, null, FixedUtcNow.AddMinutes(5), "123456", FixedUtcNow));
            Assert.IsFalse(MfaCodeValidator.Validate(false, "   ", FixedUtcNow.AddMinutes(5), "123456", FixedUtcNow));
        }

        [Test]
        public void Validate_RejectsWhenExpiryMissing_AndBypassDisabled()
        {
            Assert.IsFalse(MfaCodeValidator.Validate(false, "123456", null, "123456", FixedUtcNow));
        }

        [Test]
        public void Validate_AcceptsCode_AtExactExpiryInstant()
        {
            // expiryUtc < utcNow fails; equal is still valid (HasValue && !(expiry < now))
            var accepted = MfaCodeValidator.Validate(
                allowAnyCode: false,
                storedCode: "123456",
                expiryUtc: FixedUtcNow,
                submittedCode: "123456",
                utcNow: FixedUtcNow);

            Assert.IsTrue(accepted);
        }

        [Test]
        public void Validate_RejectsReuse_AfterStoredCodeCleared()
        {
            // AccountSecurityService clears TwoFactorCode after successful verify / EnableMfa.
            var first = MfaCodeValidator.Validate(false, "123456", FixedUtcNow.AddMinutes(5), "123456", FixedUtcNow);
            Assert.IsTrue(first);

            var reuse = MfaCodeValidator.Validate(false, null, null, "123456", FixedUtcNow);
            Assert.IsFalse(reuse);
        }

        [Test]
        public void Validate_DemoBypass_IgnoresExpiryAndStoredCode()
        {
            Assert.IsTrue(MfaCodeValidator.Validate(true, null, FixedUtcNow.AddMinutes(-30), "42", FixedUtcNow));
            Assert.IsTrue(MfaCodeValidator.Validate(true, "999999", null, "anything", FixedUtcNow));
        }
    }

    [TestFixture]
    public class MfaEnforcementExemptionsTests
    {
        [Test]
        public void IsExempt_AccountMfaFlowActions()
        {
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "SetupMfa"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "VerifyMfa"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "SendSetupMfaCode"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("account", "resendmfacode"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "LogOff"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "Login"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "VerifyEmail"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "ChangePassword"));
        }

        [Test]
        public void IsExempt_CaptchaAndHome_EntireControllers()
        {
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Captcha", "Index"));
            Assert.IsTrue(MfaEnforcementExemptions.IsExemptFromForcedSetup("Home", "Index"));
        }

        [Test]
        public void IsExempt_RejectsOtherAccountActions_AndUnknownControllers()
        {
            Assert.IsFalse(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", "Index"));
            Assert.IsFalse(MfaEnforcementExemptions.IsExemptFromForcedSetup("Account", null));
            Assert.IsFalse(MfaEnforcementExemptions.IsExemptFromForcedSetup("Assets", "Index"));
            Assert.IsFalse(MfaEnforcementExemptions.IsExemptFromForcedSetup(null, "Login"));
            Assert.IsFalse(MfaEnforcementExemptions.IsExemptFromForcedSetup("  ", "Login"));
        }
    }
}
