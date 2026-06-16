using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Mail;
using AssetManagement.Application.Contracts;

namespace AssetManagement.Infrastructure.Services
{
    public class EmailService : IEmailService
    {
        private const string ProductName = "Asset Management Module";
        private const string PublisherName = "Nanosoft";

        private readonly IPlatformSettingsService _settings;

        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;
        private readonly bool _enableSsl;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IPlatformSettingsService settings)
        {
            _settings = settings;
            _smtpHost = ReadSetting("SmtpHost", ConfigurationManager.AppSettings["SmtpHost"]);
            _smtpPort = ReadIntSetting("SmtpPort", ConfigurationManager.AppSettings["SmtpPort"], 587);
            _smtpUser = ReadSetting("SmtpUser", ConfigurationManager.AppSettings["SmtpUser"]);
            _smtpPass = ReadSetting("SmtpPassword", ConfigurationManager.AppSettings["SmtpPassword"]);
            _enableSsl = ReadBoolSetting("SmtpEnableSsl", ConfigurationManager.AppSettings["SmtpEnableSsl"], true);
            _fromEmail = ReadSetting("FromEmail", ConfigurationManager.AppSettings["FromEmail"], "noreply@example.com");
            _fromName = ReadSetting("FromName", ConfigurationManager.AppSettings["FromName"], ProductName);
        }

        public bool IsConfigured
        {
            get { return !string.IsNullOrWhiteSpace(_smtpHost); }
        }

        public void SendPasswordResetEmail(string to, string resetLink)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

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

            SendCore(to, subject, body, false);
        }

        public void SendMfaCodeEmail(string to, string code)
        {
            if (string.IsNullOrWhiteSpace(to))
            {
                return;
            }

            var subject = "Your Verification Code - " + ProductName;
            var body = string.Format(@"
<p>Verification is required for your account access.</p>
<p>Your 6-digit code:</p>
<p style=""font-size:24px;font-weight:bold;letter-spacing:4px;"">{0}</p>
<p>This code expires in 10 minutes.</p>
<p>If you did not attempt to sign in, secure your account immediately.</p>",
                code ?? string.Empty);

            SendCore(to, subject, body, true);
        }

        private void SendCore(string to, string subject, string body, bool critical)
        {
            if (!IsConfigured)
            {
                return;
            }

            try
            {
                using (var client = new SmtpClient(_smtpHost, _smtpPort))
                {
                    client.EnableSsl = _enableSsl;
                    client.UseDefaultCredentials = false;
                    client.Timeout = 10000;

                    if (!string.IsNullOrEmpty(_smtpUser) || !string.IsNullOrEmpty(_smtpPass))
                    {
                        client.Credentials = new NetworkCredential(_smtpUser, _smtpPass);
                    }

                    using (var message = new MailMessage())
                    {
                        message.From = new MailAddress(_fromEmail, _fromName);
                        message.To.Add(to.Trim());
                        message.Subject = subject ?? string.Empty;
                        message.Body = WrapHtml(body ?? string.Empty);
                        message.IsBodyHtml = true;
                        client.Send(message);
                    }
                }
            }
            catch (Exception ex)
            {
                LogEmailFailure(to, ex);
                if (critical)
                {
                    throw;
                }
            }
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

        private static void LogEmailFailure(string to, Exception ex)
        {
            var message = "Email sending failed to " + (to ?? string.Empty) + ": " + (ex == null ? "unknown error" : ex.Message);
            Trace.WriteLine(message);
            Debug.WriteLine(message);

            try
            {
                var logPath = AppDomain.CurrentDomain.BaseDirectory + "email_errors.txt";
                File.AppendAllText(logPath, string.Format("[{0}] {1}{2}", DateTime.Now, message, Environment.NewLine));
            }
            catch (Exception)
            {
                // Best-effort local log only.
            }
        }
    }
}
