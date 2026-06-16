using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace AssetManagement.Infrastructure.Services
{
    public static class WebhookHttpClient
    {
        private const int RequestTimeoutMs = 30000;

        public static void PostSignedPayload(string targetUrl, string secret, string payloadJson)
        {
            var request = (HttpWebRequest)WebRequest.Create(targetUrl);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = RequestTimeoutMs;
            request.UserAgent = "AssetManagement-Webhooks/1.0";

            if (!string.IsNullOrWhiteSpace(secret))
            {
                request.Headers["X-Webhook-Signature"] = ComputeSignature(secret, payloadJson);
            }

            var payloadBytes = Encoding.UTF8.GetBytes(payloadJson ?? string.Empty);
            request.ContentLength = payloadBytes.Length;

            using (var stream = request.GetRequestStream())
            {
                stream.Write(payloadBytes, 0, payloadBytes.Length);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            {
                var statusCode = (int)response.StatusCode;
                if (statusCode < 200 || statusCode >= 300)
                {
                    throw new InvalidOperationException("Webhook endpoint returned HTTP " + statusCode + ".");
                }
            }
        }

        public static string ComputeSignature(string secret, string payloadJson)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret ?? string.Empty)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson ?? string.Empty));
                return "sha256=" + BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
