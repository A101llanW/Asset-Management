using System;
using System.Collections.Generic;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Email;
using AssetManagement.Infrastructure.Services;
using NUnit.Framework;

namespace AssetManagement.Tests.Email
{
    [TestFixture]
    public class SmtpSettingsResolutionTests
    {
        private static SmtpSettings Platform()
        {
            return SmtpSettings.ForPlatform("smtp.platform.test", 587, true, "platform-user", "platform-secret", "noreply@platform.test", "Platform Name", true);
        }

        private static OrganizationSmtpSettingsRecord Complete()
        {
            return new OrganizationSmtpSettingsRecord
            {
                OrganizationId = 13,
                Host = " 127.0.0.1 ",
                Port = 2525,
                UseSsl = false,
                Username = "sinkuser",
                PasswordEncrypted = "v2:CIPHER",
                FromAddress = "testwf@example.test",
                FromDisplayName = "Test-WF Mail",
                IsEnabled = true,
                IsActive = true
            };
        }

        private sealed class FakeProtector : ISecretProtector
        {
            public int UnprotectCalls;
            public bool Throw;

            public string Protect(string clearText)
            {
                return "v2:" + clearText.Length;
            }

            public string Unprotect(string protectedText)
            {
                UnprotectCalls++;
                if (Throw)
                {
                    throw new System.Security.Cryptography.CryptographicException("bad mac");
                }

                return protectedText == "v2:CIPHER" ? "clear-pass" : null;
            }
        }

        [Test]
        public void EnabledAndComplete_UsesOrganization_WithLazyPassword()
        {
            var protector = new FakeProtector();
            var result = SmtpSettingsResolution.Resolve(Complete(), Platform(), protector, true);

            Assert.That(result.Source, Is.EqualTo(SmtpSettingsSource.Organization));
            Assert.That(result.OrganizationId, Is.EqualTo(13));
            Assert.That(result.Host, Is.EqualTo("127.0.0.1"));
            Assert.That(result.Port, Is.EqualTo(2525));
            Assert.That(result.EnableSsl, Is.False);
            Assert.That(result.UserName, Is.EqualTo("sinkuser"));
            Assert.That(result.FromEmail, Is.EqualTo("testwf@example.test"));
            Assert.That(result.FromName, Is.EqualTo("Test-WF Mail"));
            Assert.That(result.IsDeliverable, Is.True);
            Assert.That(protector.UnprotectCalls, Is.EqualTo(0), "password must not be decrypted until a message is sent");
            Assert.That(result.GetPassword(), Is.EqualTo("clear-pass"));
            Assert.That(result.GetPassword(), Is.EqualTo("clear-pass"));
            Assert.That(protector.UnprotectCalls, Is.EqualTo(1));
            Assert.That(result.DescribeRoute(), Is.EqualTo("company SMTP 127.0.0.1:2525"));
            Assert.That(result.ToString().Contains("clear-pass"), Is.False);
        }

        [Test]
        public void NoUsernameNoPassword_IsComplete()
        {
            var record = Complete();
            record.Username = null;
            record.PasswordEncrypted = null;
            var result = SmtpSettingsResolution.Resolve(record, Platform(), new FakeProtector(), true);
            Assert.That(result.Source, Is.EqualTo(SmtpSettingsSource.Organization));
            Assert.That(result.GetPassword(), Is.Null);
        }

        [Test]
        public void NoRow_FallsBackToPlatform()
        {
            var result = SmtpSettingsResolution.Resolve(null, Platform(), new FakeProtector(), true);
            Assert.That(result.Source, Is.EqualTo(SmtpSettingsSource.Platform));
            Assert.That(result.Host, Is.EqualTo("smtp.platform.test"));
            Assert.That(result.GetPassword(), Is.EqualTo("platform-secret"));
            Assert.That(result.Reason, Is.EqualTo("No company SMTP settings are saved."));
        }

        private static IEnumerable<TestCaseData> IncompleteCases()
        {
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.IsEnabled = false), "Company SMTP is turned off.").SetName("Disabled");
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.IsActive = false), "Company SMTP settings were removed.").SetName("SoftDeleted");
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.Host = "  "), "Company SMTP host is missing.").SetName("NoHost");
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.Port = 0), "Company SMTP port must be between 1 and 65535.").SetName("PortZero");
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.Port = 70000), "Company SMTP port must be between 1 and 65535.").SetName("PortTooHigh");
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.FromAddress = null), "Company SMTP From address is missing.").SetName("NoFrom");
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.FromAddress = "not-an-email"), "Company SMTP From address is not a valid email address.").SetName("BadFrom");
            yield return new TestCaseData((Action<OrganizationSmtpSettingsRecord>)(r => r.PasswordEncrypted = null), "Company SMTP username is set but no password is saved.").SetName("UserWithoutPassword");
        }

        [TestCaseSource("IncompleteCases")]
        public void IncompleteOrDisabled_FallsBackToPlatform(Action<OrganizationSmtpSettingsRecord> mutate, string expectedReason)
        {
            var record = Complete();
            mutate(record);
            var result = SmtpSettingsResolution.Resolve(record, Platform(), new FakeProtector(), true);
            Assert.That(result.Source, Is.EqualTo(SmtpSettingsSource.Platform));
            Assert.That(result.Host, Is.EqualTo("smtp.platform.test"));
            Assert.That(result.Reason, Is.EqualTo(expectedReason));
            Assert.That(SmtpSettingsResolution.IsUsable(record), Is.False);
        }

        [Test]
        public void PasswordButNoProtector_FallsBack()
        {
            var result = SmtpSettingsResolution.Resolve(Complete(), Platform(), null, true);
            Assert.That(result.Source, Is.EqualTo(SmtpSettingsSource.Platform));
        }

        [Test]
        public void RequireEnabledFalse_LetsTestEmailUseSavedSettingsBeforeTurningOn()
        {
            var record = Complete();
            record.IsEnabled = false;
            var result = SmtpSettingsResolution.Resolve(record, null, new FakeProtector(), false);
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Source, Is.EqualTo(SmtpSettingsSource.Organization));
        }

        [Test]
        public void IncompleteWithoutPlatformDefault_ReturnsNull()
        {
            var record = Complete();
            record.Host = null;
            Assert.That(SmtpSettingsResolution.Resolve(record, null, new FakeProtector(), false), Is.Null);
        }

        [Test]
        public void BlankDisplayName_UsesPlatformFromName()
        {
            var record = Complete();
            record.FromDisplayName = " ";
            var result = SmtpSettingsResolution.Resolve(record, Platform(), new FakeProtector(), true);
            Assert.That(result.FromName, Is.EqualTo("Platform Name"));
        }

        [Test]
        public void DecryptFailure_SurfacesOnlyWhenPasswordIsRead_AndIsNotCached()
        {
            var protector = new FakeProtector { Throw = true };
            var result = SmtpSettingsResolution.Resolve(Complete(), Platform(), protector, true);
            Assert.That(result.Source, Is.EqualTo(SmtpSettingsSource.Organization));
            Assert.Throws<System.Security.Cryptography.CryptographicException>(() => result.GetPassword());
            protector.Throw = false;
            Assert.That(result.GetPassword(), Is.EqualTo("clear-pass"));
        }

        // ---- Resolver service (store + rules) ----

        private sealed class FakeStore : IOrganizationSmtpSettingsStore
        {
            public readonly Dictionary<int, OrganizationSmtpSettingsRecord> Rows = new Dictionary<int, OrganizationSmtpSettingsRecord>();
            public int Gets;

            public bool IsAvailable()
            {
                return true;
            }

            public OrganizationSmtpSettingsRecord Get(int organizationId)
            {
                Gets++;
                OrganizationSmtpSettingsRecord row;
                return Rows.TryGetValue(organizationId, out row) ? row : null;
            }

            public void Save(OrganizationSmtpSettingsRecord record, string actorUserId)
            {
                Rows[record.OrganizationId] = record;
            }
        }

        [Test]
        public void Resolver_NullOrganization_IsPlatform_WithoutLookup()
        {
            var store = new FakeStore();
            store.Rows[13] = Complete();
            var resolver = new OrganizationSmtpSettingsResolver(store, new FakeProtector());
            var platform = Platform();
            Assert.That(resolver.Resolve(null, platform), Is.SameAs(platform));
            Assert.That(store.Gets, Is.EqualTo(0));
        }

        [Test]
        public void Resolver_PicksOwnSmtpForTestWf_AndPlatformForNis()
        {
            var store = new FakeStore();
            store.Rows[13] = Complete();
            var resolver = new OrganizationSmtpSettingsResolver(store, new FakeProtector());
            Assert.That(resolver.Resolve(13, Platform()).Source, Is.EqualTo(SmtpSettingsSource.Organization));
            Assert.That(resolver.Resolve(21, Platform()).Source, Is.EqualTo(SmtpSettingsSource.Platform));
        }

        // ---- EmailService routing ----

        private sealed class FakePlatformSettings : IPlatformSettingsService
        {
            private readonly Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "SmtpHost", "smtp.platform.test" },
                { "SmtpPort", "2526" },
                { "SmtpEnableSsl", "false" },
                { "FromEmail", "noreply@platform.test" },
                { "FromName", "Platform Name" }
            };

            public string GetSetting(string key, string defaultValue)
            {
                string value;
                return _values.TryGetValue(key, out value) ? value : defaultValue;
            }

            public void SetSetting(string key, string value, string description)
            {
                _values[key] = value;
            }

            public string GetExternalBaseUrl()
            {
                return null;
            }
        }

        private sealed class ThrowingResolver : ISmtpSettingsResolver
        {
            public SmtpSettings Resolve(int? organizationId, SmtpSettings platformDefault)
            {
                throw new InvalidOperationException("db down");
            }
        }

        [Test]
        public void EmailService_ForOrganization_RoutesPerOrganization()
        {
            var store = new FakeStore();
            store.Rows[13] = Complete();
            var service = new EmailService(new FakePlatformSettings(), new OrganizationSmtpSettingsResolver(store, new FakeProtector()));

            Assert.That(service.UsesOrganizationSmtp, Is.False);
            Assert.That(service.RouteDescription, Is.EqualTo("platform default SMTP"));

            var testWf = service.ForOrganization(13);
            Assert.That(testWf.UsesOrganizationSmtp, Is.True);
            Assert.That(testWf.IsConfigured, Is.True);
            Assert.That(testWf.RouteDescription, Is.EqualTo("company SMTP 127.0.0.1:2525"));

            var nis = service.ForOrganization(21);
            Assert.That(nis.UsesOrganizationSmtp, Is.False);
            Assert.That(nis, Is.SameAs(service));

            Assert.That(service.ForOrganization(null), Is.SameAs(service));
            Assert.That(testWf.ForOrganization(null).UsesOrganizationSmtp, Is.False, "platform email from an org-scoped sender goes back to the default");
        }

        [Test]
        public void EmailService_DisablingCompanySmtp_FallsBackImmediately()
        {
            var store = new FakeStore();
            store.Rows[13] = Complete();
            var service = new EmailService(new FakePlatformSettings(), new OrganizationSmtpSettingsResolver(store, new FakeProtector()));
            Assert.That(service.ForOrganization(13).UsesOrganizationSmtp, Is.True);
            store.Rows[13].IsEnabled = false;
            Assert.That(service.ForOrganization(13).UsesOrganizationSmtp, Is.False);
        }

        [Test]
        public void EmailService_ResolverFailure_FallsBackToPlatform()
        {
            var service = new EmailService(new FakePlatformSettings(), new ThrowingResolver());
            var sender = service.ForOrganization(13);
            Assert.That(sender.UsesOrganizationSmtp, Is.False);
            Assert.That(sender.IsConfigured, Is.True);
        }

        [Test]
        public void EmailService_WithoutResolver_AlwaysPlatform()
        {
            var service = new EmailService(new FakePlatformSettings());
            Assert.That(service.ForOrganization(13).UsesOrganizationSmtp, Is.False);
        }

        [Test]
        public void DescribeSendError_UnwrapsRetryWrapper_AndHasNoStackTrace()
        {
            var smtp = new System.Net.Mail.SmtpException("Failure sending mail.", new System.Net.Sockets.SocketException(10061));
            var wrapped = new InvalidOperationException("Email delivery failed after 1 attempt(s).", smtp);
            var text = EmailService.DescribeSendError(wrapped);
            Assert.That(text.StartsWith("Failure sending mail", StringComparison.Ordinal), Is.True, text);
            Assert.That(text.Contains(" at "), Is.False, text);
            Assert.That(text.Contains("\n"), Is.False);
        }

        // ---- Audit text ----

        [Test]
        public void Audit_ActionFor_CoversCreateEnableDisableClearUpdate()
        {
            var before = Complete();
            Assert.That(SmtpSettingsAudit.ActionFor(null, Complete(), SmtpSettingsAudit.PasswordChanged), Is.EqualTo(SmtpSettingsAudit.ActionCreate));

            var disabled = before.Clone();
            disabled.IsEnabled = false;
            Assert.That(SmtpSettingsAudit.ActionFor(before, disabled, SmtpSettingsAudit.PasswordUnchanged), Is.EqualTo(SmtpSettingsAudit.ActionDisable));
            Assert.That(SmtpSettingsAudit.ActionFor(disabled, before, SmtpSettingsAudit.PasswordUnchanged), Is.EqualTo(SmtpSettingsAudit.ActionEnable));

            var cleared = before.Clone();
            cleared.PasswordEncrypted = null;
            Assert.That(SmtpSettingsAudit.ActionFor(before, cleared, SmtpSettingsAudit.PasswordCleared), Is.EqualTo(SmtpSettingsAudit.ActionClearPassword));

            var credentialsRemoved = cleared.Clone();
            credentialsRemoved.Username = null;
            Assert.That(SmtpSettingsAudit.ActionFor(before, credentialsRemoved, SmtpSettingsAudit.PasswordCleared), Is.EqualTo(SmtpSettingsAudit.ActionClearPassword));

            var disabledAndCleared = cleared.Clone();
            disabledAndCleared.IsEnabled = false;
            Assert.That(SmtpSettingsAudit.ActionFor(before, disabledAndCleared, SmtpSettingsAudit.PasswordCleared), Is.EqualTo(SmtpSettingsAudit.ActionDisable));

            var moved = before.Clone();
            moved.Port = 587;
            Assert.That(SmtpSettingsAudit.ActionFor(before, moved, SmtpSettingsAudit.PasswordUnchanged), Is.EqualTo(SmtpSettingsAudit.ActionUpdate));
        }

        [Test]
        public void Audit_Snapshot_NeverContainsPasswordOrCiphertext()
        {
            var record = Complete();
            var text = SmtpSettingsAudit.Snapshot(record, SmtpSettingsAudit.PasswordChanged);
            Assert.That(text.Contains("v2:CIPHER"), Is.False);
            Assert.That(text.Contains("CIPHER"), Is.False);
            Assert.That(text.Contains("clear-pass"), Is.False);
            Assert.That(text.Contains("Password=(set)"), Is.True);
            Assert.That(text.Contains("PasswordChange=changed"), Is.True);
        }
    }
}
