using System;

namespace AssetManagement.Application.Email
{
    public enum SmtpSettingsSource
    {
        /// <summary>Platform default: SystemSetting (OrganizationId IS NULL) with Web.config / smtp.secrets.config fallback.</summary>
        Platform = 0,

        /// <summary>The organization's own OrganizationSmtpSettings row (enabled and complete).</summary>
        Organization = 1
    }

    /// <summary>
    /// The SMTP server one email goes through. The password is read lazily (company passwords are decrypted
    /// only when a message is actually sent) and is never part of <see cref="DescribeRoute"/> or ToString().
    /// </summary>
    public sealed class SmtpSettings
    {
        private readonly Func<string> _passwordProvider;
        private readonly object _passwordGate = new object();
        private bool _passwordResolved;
        private string _password;

        public SmtpSettings(
            SmtpSettingsSource source,
            int? organizationId,
            string host,
            int port,
            bool enableSsl,
            string userName,
            Func<string> passwordProvider,
            string fromEmail,
            string fromName,
            bool fromEmailIsExplicit,
            string reason)
        {
            Source = source;
            OrganizationId = organizationId;
            Host = host;
            Port = port;
            EnableSsl = enableSsl;
            UserName = userName;
            _passwordProvider = passwordProvider;
            FromEmail = fromEmail;
            FromName = fromName;
            FromEmailIsExplicit = fromEmailIsExplicit;
            Reason = reason;
        }

        public static SmtpSettings ForPlatform(
            string host,
            int port,
            bool enableSsl,
            string userName,
            string password,
            string fromEmail,
            string fromName,
            bool fromEmailIsExplicit)
        {
            return new SmtpSettings(
                SmtpSettingsSource.Platform,
                null,
                host,
                port,
                enableSsl,
                userName,
                () => password,
                fromEmail,
                fromName,
                fromEmailIsExplicit,
                "Platform default SMTP.");
        }

        public SmtpSettingsSource Source { get; private set; }

        public int? OrganizationId { get; private set; }

        public string Host { get; private set; }

        public int Port { get; private set; }

        public bool EnableSsl { get; private set; }

        public string UserName { get; private set; }

        public string FromEmail { get; private set; }

        public string FromName { get; private set; }

        public bool FromEmailIsExplicit { get; private set; }

        /// <summary>Why this route was chosen (e.g. "Company SMTP is turned off."). Never contains secrets.</summary>
        public string Reason { get; private set; }

        public bool IsOrganization
        {
            get { return Source == SmtpSettingsSource.Organization; }
        }

        public bool HasHost
        {
            get { return !string.IsNullOrWhiteSpace(Host); }
        }

        /// <summary>Host and an explicit From address are set (same rule the platform sender always used).</summary>
        public bool IsDeliverable
        {
            get { return HasHost && FromEmailIsExplicit && !string.IsNullOrWhiteSpace(FromEmail); }
        }

        /// <summary>Clear password for the SMTP login, or null. Company passwords are decrypted here (may throw).</summary>
        public string GetPassword()
        {
            lock (_passwordGate)
            {
                if (!_passwordResolved)
                {
                    _password = _passwordProvider == null ? null : _passwordProvider();
                    _passwordResolved = true;
                }

                return _password;
            }
        }

        /// <summary>Same server settings with a different <see cref="Reason"/>.</summary>
        public SmtpSettings WithReason(string reason)
        {
            return new SmtpSettings(Source, OrganizationId, Host, Port, EnableSsl, UserName, GetPasswordProviderForCopy(), FromEmail, FromName, FromEmailIsExplicit, reason);
        }

        /// <summary>"company SMTP host:port" or "platform default SMTP". No credentials.</summary>
        public string DescribeRoute()
        {
            if (Source == SmtpSettingsSource.Organization)
            {
                return "company SMTP " + Host + ":" + Port;
            }

            return HasHost ? "platform default SMTP" : "platform default SMTP (not configured)";
        }

        public override string ToString()
        {
            return DescribeRoute();
        }

        private Func<string> GetPasswordProviderForCopy()
        {
            var provider = _passwordProvider;
            return () => provider == null ? null : provider();
        }
    }
}
