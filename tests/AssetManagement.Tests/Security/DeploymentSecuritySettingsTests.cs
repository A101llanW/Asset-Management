using AssetManagement.Application.Contracts;
using AssetManagement.Application.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class DeploymentSecuritySettingsTests
    {
        [Test]
        public void RequireMfaForAllUsers_DefaultsToFalseWhenUnset()
        {
            Assert.IsFalse(DeploymentSecuritySettings.RequireMfaForAllUsers);
        }

        [Test]
        public void RequireHttpsRedirect_DefaultsToFalseWhenUnset()
        {
            Assert.IsFalse(DeploymentSecuritySettings.RequireHttpsRedirect);
        }

        [Test]
        public void MfaAllowAnyCode_DefaultsToFalseWhenUnset()
        {
            Assert.IsFalse(DeploymentSecuritySettings.MfaAllowAnyCode);
        }

        [Test]
        public void RequiresSmtpForAuthEmails_IsInverseOfMfaAllowAnyCode()
        {
            Assert.AreEqual(!DeploymentSecuritySettings.MfaAllowAnyCode, DeploymentSecuritySettings.RequiresSmtpForAuthEmails);
        }
    }

    [TestFixture]
    public class EmailConfigurationStatusTests
    {
        [Test]
        public void IsReadyForAuthDelivery_RequiresHostAndFromEmail()
        {
            var status = new EmailConfigurationStatus
            {
                HasSmtpHost = true,
                HasFromEmail = true,
                RequiresDelivery = true
            };

            Assert.IsTrue(status.IsReadyForAuthDelivery);
            Assert.IsFalse(status.IsBlockingProductionAuth);
            Assert.AreEqual(0, status.GetMissingRequirements().Length);
        }

        [Test]
        public void IsBlockingProductionAuth_WhenDeliveryRequiredAndHostMissing()
        {
            var status = new EmailConfigurationStatus
            {
                HasSmtpHost = false,
                HasFromEmail = false,
                RequiresDelivery = true
            };

            Assert.IsFalse(status.IsReadyForAuthDelivery);
            Assert.IsTrue(status.IsBlockingProductionAuth);
            CollectionAssert.Contains(status.GetMissingRequirements(), "SmtpHost");
            CollectionAssert.Contains(status.GetMissingRequirements(), "FromEmail");
        }
    }

    [TestFixture]
    public class SmtpStartupValidatorTests
    {
        private sealed class StubEmailService : IEmailService
        {
            private readonly EmailConfigurationStatus _status;

            public StubEmailService(EmailConfigurationStatus status)
            {
                _status = status;
            }

            public bool IsConfigured
            {
                get { return _status.IsReadyForAuthDelivery; }
            }

            public EmailConfigurationStatus GetConfigurationStatus()
            {
                return _status;
            }

            public void SendPasswordResetEmail(string to, string resetLink)
            {
            }

            public void SendMfaCodeEmail(string to, string code)
            {
            }

            public bool SendTestEmail(string to, out string errorMessage)
            {
                errorMessage = null;
                return true;
            }
        }

        [Test]
        public void ValidateForStartup_ReturnsNullWhenSmtpConfigured()
        {
            var emailService = new StubEmailService(new EmailConfigurationStatus
            {
                HasSmtpHost = true,
                HasFromEmail = true,
                RequiresDelivery = true
            });

            Assert.IsNull(SmtpStartupValidator.ValidateForStartup(emailService));
        }

        [Test]
        public void ValidateForStartup_ReturnsMessageWhenSmtpMissingInProductionMode()
        {
            var emailService = new StubEmailService(new EmailConfigurationStatus
            {
                HasSmtpHost = false,
                HasFromEmail = false,
                RequiresDelivery = true
            });

            var message = SmtpStartupValidator.ValidateForStartup(emailService);
            Assert.IsNotNull(message);
            StringAssert.Contains("SmtpHost", message);
        }
    }
}
