using System;
using System.Security.Cryptography;
using System.Text;

namespace AssetManagement.Application.Helpers
{
    public static class InvitationTokenHelper
    {
        private const string HashKey = "asset-management-invite";

        public static string GenerateToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", string.Empty)
                .Replace("/", string.Empty)
                .TrimEnd('=');
        }

        public static string ComputeTokenHash(string token)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HashKey)))
            {
                return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(token ?? string.Empty)));
            }
        }
    }
}
