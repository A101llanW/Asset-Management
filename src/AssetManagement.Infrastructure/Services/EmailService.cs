using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Email;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;

namespace AssetManagement.Infrastructure.Services
{
    /// <summary>
    /// Sends all application email. The instance the container hands out uses the platform default SMTP
    /// (SystemSetting with Web.config / smtp.secrets.config fallback). Organization-owned email goes through
    /// <see cref="ForOrganization"/>, which switches to the organization's own SMTP when it is enabled and
    /// complete (see <see cref="SmtpSettingsResolution"/>) and otherwise keeps the platform default.
    /// Passwords are never logged.
    /// </summary>
    public class EmailService : IEmailService
    {
        private const string ProductName = "Asset Management Module";
        private const string PublisherName = "Nanosoft";
        private const string DefaultFromEmail = "noreply@example.com";
        private const int MaxSendAttempts = 3;
        private const int TestSendAttempts = 1;
        private const int RetryDelayMilliseconds = 500;

        private readonly IPlatformSettingsService _settings;
        private readonly ISmtpSettingsResolver _resolver;
        private readonly SmtpSettings _platform;
        private readonly SmtpSettings _active;
        private readonly bool _externalBaseUrlIsConfigured;

        public EmailService(IPlatformSettingsService settings)
            : this(settings, null)
        {
        }

        public EmailService(IPlatformSettingsService settings, ISmtpSettingsResolver resolver)
        {
            _settings = settings;
            _resolver = resolver;
            _platform = ReadPlatformSettings();
            _active = _platform;
            _externalBaseUrlIsConfigured = HasExplicitSetting("ExternalBaseUrl", ConfigurationManager.AppSettings["ExternalBaseUrl"]);
        }

        private EmailService(EmailService parent, SmtpSettings active)
        {
            _settings = parent._settings;
            _resolver = parent._resolver;
            _platform = parent._platform;
            _active = active ?? parent._platform;
            _externalBaseUrlIsConfigured = parent._externalBaseUrlIsConfigured;
        }

        public bool IsConfigured
        {
            get { return _active.IsDeliverable; }
        }

        public bool UsesOrganizationSmtp
        {
            get { return _active.IsOrganization; }
        }

        public string RouteDescription
        {
            get { return _active.DescribeRoute(); }
        }

        /// <summary>The settings this sender uses (for diagnostics and tests; the password stays lazy).</summary>
        public SmtpSettings ActiveSettings
        {
            get { return _active; }
        }

        public IEmailService ForOrganization(int? organizationId)
        {
            if (!organizationId.HasValue || organizationId.Value <= 0 || _resolver == null)
            {
                return PlatformSender();
            }

            SmtpSettings resolved;
            try
            {
                resolved = _resolver.Resolve(organizationId, _platform);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "Company SMTP lookup failed for organization {0}; using the platform default: {1}",
                    organizationId,
                    ex.Message);
                resolved = null;
            }

            if (resolved == null || !resolved.IsOrganization)
            {
                return PlatformSender();
            }

            return new EmailService(this, resolved);
        }

        public IEmailService ForSettings(SmtpSettings settings)
        {
            return settings == null ? PlatformSender() : new EmailService(this, settings);
        }

        public EmailConfigurationStatus GetConfigurationStatus()
        {
            return new EmailConfigurationStatus
            {
                HasSmtpHost = _active.HasHost,
                HasFromEmail = _active.FromEmailIsExplicit && !string.IsNullOrWhiteSpace(_active.FromEmail),
                HasExternalBaseUrl = _externalBaseUrlIsConfigured,
                RequiresDelivery = DeploymentSecuritySettings.RequiresSmtpForAuthEmails
            };
        }

        public void SendPasswordResetEmail(string to, string resetLink)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            EnsureConfiguredForDelivery("password reset");

            var subject = "Password Reset Request - " + ProductName;
            var body = string.Format(@"
<p>Hello,</p>
<p>We received a request to reset your password for your {1} account.</p>
<p><strong>This link expires in 24 hours.</strong></p>
<p><a href=""{0}"">Reset your password</a></p>
<p>If you did not request this reset, ignore this email.</p>
<p style=""word-break:break-all;"">{0}</p>
<p>&copy; {2} {3}</p>",
                resetLink ?? string.Empty,
                ProductName,
                DateTime.UtcNow.Year,
                PublisherName);

            SendCore(to, subject, body, critical: true);
        }

        public void SendUserInvitationEmail(string to, string inviteLink, string organizationName)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            EnsureConfiguredForDelivery("user invitation");

            var orgLabel = string.IsNullOrWhiteSpace(organizationName) ? ProductName : organizationName;
            var subject = "You're invited to join " + orgLabel;
            var body = string.Format(@"
<p>Hello,</p>
<p>You have been invited to create an account for <strong>{1}</strong> on {2}.</p>
<p><strong>This invitation link expires in 7 days and can only be used once.</strong></p>
<p><a href=""{0}"">Accept invitation and create your account</a></p>
<p>If you were not expecting this invitation, you can ignore this email.</p>
<p style=""word-break:break-all;"">{0}</p>
<p>&copy; {3} {4}</p>",
                inviteLink ?? string.Empty,
                orgLabel,
                ProductName,
                DateTime.UtcNow.Year,
                PublisherName);

            SendCore(to, subject, body, critical: true);
        }

        public void SendMfaCodeEmail(string to, string code)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            EnsureConfiguredForDelivery("MFA");

            var subject = "Your Verification Code - " + ProductName;
            var body = string.Format(@"
<p>Verification is required for your account access.</p>
<p>Your 6-digit code:</p>
<p style=""font-size:24px;font-weight:bold;letter-spacing:4px;"">{0}</p>
<p>This code expires in 10 minutes.</p>
<p>If you did not attempt to sign in, secure your account immediately.</p>",
                code ?? string.Empty);

            SendCore(to, subject, body, critical: true);
        }

        /// <summary>
        /// One attempt (no retries) through exactly this sender's SMTP, so a wrong company setting shows its
        /// real error quickly instead of silently using the platform default. Errors carry no stack trace.
        /// </summary>
        public bool SendTestEmail(string to, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(to))
            {
                errorMessage = "Enter a recipient email address.";
                return false;
            }

            if (!IsConfigured)
            {
                errorMessage = GetConfigurationStatus().GetSummary();
                return false;
            }

            try
            {
                var subject = "SMTP Test - " + ProductName;
                var body = string.Format(
                    "<p>This is a test message from {0} sent at {1} through {2}.</p><p>If you received this email, SMTP is working.</p>",
                    ProductName,
                    KenyaTimeHelper.FormatUtc(DateTime.UtcNow) + " " + KenyaTimeHelper.DisplayAbbreviation,
                    System.Security.SecurityElement.Escape(_active.DescribeRoute()));
                SendWithRetries(to.Trim(), subject, body, critical: true, maxAttempts: TestSendAttempts, allowPlatformFallback: false);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = DescribeSendError(ex);
                return false;
            }
        }

        public bool TrySendSecurityAlert(string to, string subject, string body)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return false;
            }

            if (!IsConfigured)
            {
                return false;
            }

            try
            {
                SendCore(to.Trim(), subject ?? "Security alert", body ?? string.Empty, critical: false);
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Security alert email failed for {0}: {1}", to, ex.Message);
                return false;
            }
        }

        /// <summary>Short user-facing SMTP error: the outer message plus the innermost cause, no stack trace.</summary>
        public static string DescribeSendError(Exception ex)
        {
            if (ex == null)
            {
                return "Email could not be sent.";
            }

            var outer = ex;
            if (outer is InvalidOperationException && outer.InnerException != null
                && outer.Message.StartsWith("Email delivery failed", StringComparison.Ordinal))
            {
                outer = outer.InnerException;
            }

            var inner = outer;
            while (inner.InnerException != null)
            {
                inner = inner.InnerException;
            }

            var text = outer.Message ?? string.Empty;
            if (!ReferenceEquals(inner, outer) && !string.IsNullOrWhiteSpace(inner.Message)
                && text.IndexOf(inner.Message, StringComparison.Ordinal) < 0)
            {
                text = text.TrimEnd('.', ' ') + ": " + inner.Message;
            }

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length > 400)
            {
                text = text.Substring(0, 400) + "...";
            }

            return string.IsNullOrWhiteSpace(text) ? "Email could not be sent." : text;
        }

        private IEmailService PlatformSender()
        {
            return ReferenceEquals(_active, _platform) ? this : new EmailService(this, _platform);
        }

        private void EnsureConfiguredForDelivery(string purpose)
        {
            if (IsConfigured)
            {
                return;
            }

            if (DeploymentSecuritySettings.RequiresSmtpForAuthEmails)
            {
                throw new InvalidOperationException(
                    "SMTP is not configured for " + purpose + ". Missing: "
                    + string.Join(", ", GetConfigurationStatus().GetMissingRequirements())
                    + ".");
            }
        }

        private void SendCore(string to, string subject, string body, bool critical)
        {
            SendWithRetries(to, subject, body, critical, MaxSendAttempts, allowPlatformFallback: true);
        }

        private void SendWithRetries(string to, string subject, string body, bool critical, int maxAttempts, bool allowPlatformFallback)
        {
            if (!IsConfigured)
            {
                if (critical && DeploymentSecuritySettings.RequiresSmtpForAuthEmails)
                {
                    throw new InvalidOperationException("SMTP is not configured.");
                }

                return;
            }

            var settings = _active;
            string password;
            if (!TryGetPassword(settings, out password))
            {
                if (!allowPlatformFallback || !settings.IsOrganization)
                {
                    throw new InvalidOperationException("The saved SMTP password could not be read. Enter it again and save.");
                }

                Trace.TraceWarning(
                    "Company SMTP password for organization {0} could not be read; sending through the platform default instead.",
                    settings.OrganizationId);
                settings = _platform;
                if (!settings.IsDeliverable)
                {
                    if (critical && DeploymentSecuritySettings.RequiresSmtpForAuthEmails)
                    {
                        throw new InvalidOperationException("SMTP is not configured.");
                    }

                    return;
                }

                password = settings.GetPassword();
            }

            Exception lastError = null;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    SendOnce(settings, password, to, subject, body);
                    LogEmailSuccess(settings, to, subject, attempt);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    LogEmailFailure(settings, to, subject, attempt, maxAttempts, ex);
                    if (attempt < maxAttempts)
                    {
                        Thread.Sleep(RetryDelayMilliseconds);
                    }
                }
            }

            if (critical && lastError != null)
            {
                throw new InvalidOperationException("Email delivery failed after " + maxAttempts + " attempt(s).", lastError);
            }
        }

        private static bool TryGetPassword(SmtpSettings settings, out string password)
        {
            try
            {
                password = settings.GetPassword();
                return true;
            }
            catch (Exception ex)
            {
                // Type only: decryption messages are generic, but never risk echoing secret material.
                Trace.TraceWarning("SMTP password could not be read ({0}).", ex.GetType().Name);
                password = null;
                return false;
            }
        }

        private static void SendOnce(SmtpSettings settings, string password, string to, string subject, string body)
        {
            using (var client = new SmtpClient(settings.Host, settings.Port))
            {
                client.EnableSsl = settings.EnableSsl;
                client.UseDefaultCredentials = false;
                client.Timeout = 10000;

                if (!string.IsNullOrEmpty(settings.UserName) || !string.IsNullOrEmpty(password))
                {
                    client.Credentials = new NetworkCredential(settings.UserName, password);
                }

                using (var message = new MailMessage())
                {
                    message.From = new MailAddress(settings.FromEmail, settings.FromName);
                    message.To.Add(to.Trim());
                    message.Subject = subject ?? string.Empty;
                    message.Body = WrapHtml(body ?? string.Empty);
                    message.IsBodyHtml = true;
                    client.Send(message);
                }
            }
        }

        private SmtpSettings ReadPlatformSettings()
        {
            var host = ReadSetting("SmtpHost", ConfigurationManager.AppSettings["SmtpHost"]);
            var port = ReadIntSetting("SmtpPort", ConfigurationManager.AppSettings["SmtpPort"], 587);
            var user = ReadSetting("SmtpUser", ConfigurationManager.AppSettings["SmtpUser"]);
            var pass = ReadSetting("SmtpPassword", ConfigurationManager.AppSettings["SmtpPassword"]);
            var enableSsl = ReadBoolSetting("SmtpEnableSsl", ConfigurationManager.AppSettings["SmtpEnableSsl"], true);
            var fromEmailIsExplicit = HasExplicitSetting("FromEmail", ConfigurationManager.AppSettings["FromEmail"]);
            var fromEmail = ReadSetting("FromEmail", ConfigurationManager.AppSettings["FromEmail"], DefaultFromEmail);
            var fromName = ReadSetting("FromName", ConfigurationManager.AppSettings["FromName"], ProductName);
            return SmtpSettings.ForPlatform(host, port, enableSsl, user, pass, fromEmail, fromName, fromEmailIsExplicit);
        }

        private string ReadSetting(string key, string webConfigValue, string fallback)
        {
            var value = _settings == null ? null : _settings.GetSetting(key, null);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            if (!string.IsNullOrWhiteSpace(webConfigValue))
            {
                return webConfigValue;
            }

            return fallback;
        }

        private string ReadSetting(string key, string webConfigValue)
        {
            return ReadSetting(key, webConfigValue, null);
        }

        private bool HasExplicitSetting(string key, string webConfigValue)
        {
            var value = _settings == null ? null : _settings.GetSetting(key, null);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(webConfigValue);
        }

        private int ReadIntSetting(string key, string webConfigValue, int fallback)
        {
            var raw = _settings == null ? null : _settings.GetSetting(key, null);
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = webConfigValue;
            }

            int parsed;
            if (!string.IsNullOrWhiteSpace(raw) && int.TryParse(raw, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private bool ReadBoolSetting(string key, string webConfigValue, bool fallback)
        {
            var raw = _settings == null ? null : _settings.GetSetting(key, null);
            if (string.IsNullOrWhiteSpace(raw))
            {
                raw = webConfigValue;
            }

            bool parsed;
            if (!string.IsNullOrWhiteSpace(raw) && bool.TryParse(raw, out parsed))
            {
                return parsed;
            }

            return fallback;
        }

        private static string WrapHtml(string body)
        {
            return "<!DOCTYPE html><html><body style=\"font-family:Arial,sans-serif;line-height:1.5;color:#333;\">"
                + body
                + "</body></html>";
        }

        private static string DescribeOrganization(SmtpSettings settings)
        {
            return settings.OrganizationId.HasValue ? settings.OrganizationId.Value.ToString() : "platform";
        }

        private static void LogEmailSuccess(SmtpSettings settings, string to, string subject, int attempt)
        {
            // Route only (host:port / platform default); never credentials.
            var message = string.Format(
                "[{0}] Email sent to={1} subject={2} via {3} org={4} after {5} attempt(s).",
                KenyaTimeHelper.FormatLogTimestamp(DateTime.UtcNow),
                to ?? string.Empty,
                subject ?? string.Empty,
                settings.DescribeRoute(),
                DescribeOrganization(settings),
                attempt);
            Trace.WriteLine(message);
            Debug.WriteLine(message);
        }

        private static void LogEmailFailure(SmtpSettings settings, string to, string subject, int attempt, int maxAttempts, Exception ex)
        {
            var message = string.Format(
                "[{0}] Email attempt {1}/{2} failed to={3} subject={4} via {5} org={6}: {7}",
                KenyaTimeHelper.FormatLogTimestamp(DateTime.UtcNow),
                attempt,
                maxAttempts,
                to ?? string.Empty,
                subject ?? string.Empty,
                settings.DescribeRoute(),
                DescribeOrganization(settings),
                ex == null ? "unknown error" : DescribeSendError(ex));
            Trace.WriteLine(message);
            Debug.WriteLine(message);

            try
            {
                var logPath = AppDomain.CurrentDomain.BaseDirectory + "email_errors.txt";
                File.AppendAllText(logPath, message + Environment.NewLine);
            }
            catch (Exception)
            {
                // Best-effort local log only.
            }
        }
    }
}
