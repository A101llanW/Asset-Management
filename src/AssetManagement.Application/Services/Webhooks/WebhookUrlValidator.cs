using System;
using System.Net;
using System.Net.Sockets;

namespace AssetManagement.Application.Services.Webhooks
{
    public static class WebhookUrlValidator
    {
        public static void EnsureSafeWebhookUrl(string targetUrl)
        {
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                throw new InvalidOperationException("Target URL is required.");
            }

            Uri uri;
            if (!Uri.TryCreate(targetUrl.Trim(), UriKind.Absolute, out uri))
            {
                throw new InvalidOperationException("Target URL must be an absolute URL.");
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Webhook target URL must use HTTPS.");
            }

            if (string.IsNullOrWhiteSpace(uri.Host))
            {
                throw new InvalidOperationException("Webhook target URL must include a host name.");
            }

            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Webhook target URL cannot point to localhost.");
            }

            IPAddress[] addresses;
            try
            {
                addresses = Dns.GetHostAddresses(uri.Host);
            }
            catch (SocketException)
            {
                throw new InvalidOperationException("Webhook target URL host could not be resolved.");
            }

            if (addresses == null || addresses.Length == 0)
            {
                throw new InvalidOperationException("Webhook target URL host could not be resolved.");
            }

            foreach (var address in addresses)
            {
                if (IsBlockedAddress(address))
                {
                    throw new InvalidOperationException("Webhook target URL resolves to a private or restricted IP address.");
                }
            }
        }

        private static bool IsBlockedAddress(IPAddress address)
        {
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var bytes = address.GetAddressBytes();
                if (bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0)
                {
                    return true;
                }

                if (bytes[0] == 169 && bytes[1] == 254)
                {
                    return true;
                }

                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                {
                    return true;
                }

                if (bytes[0] == 192 && bytes[1] == 168)
                {
                    return true;
                }
            }

            if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            {
                return true;
            }

            return false;
        }
    }
}
