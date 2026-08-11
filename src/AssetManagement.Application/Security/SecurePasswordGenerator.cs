using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AssetManagement.Application.Security
{
    public static class SecurePasswordGenerator
    {
        private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string Lower = "abcdefghijkmnopqrstuvwxyz";
        private const string Digits = "23456789";
        private const string Symbols = "!@#$%&*?";

        public static string Generate(int length = 16)
        {
            if (length < 12)
            {
                length = 12;
            }

            var required = new[]
            {
                Pick(Upper),
                Pick(Lower),
                Pick(Digits),
                Pick(Symbols)
            };

            var all = Upper + Lower + Digits + Symbols;
            var remaining = length - required.Length;
            var password = new StringBuilder(length);
            foreach (var ch in required)
            {
                password.Append(ch);
            }

            for (var i = 0; i < remaining; i++)
            {
                password.Append(Pick(all));
            }

            return Shuffle(password.ToString());
        }

        public static string GenerateAccessToken()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            return Convert.ToBase64String(bytes)
                .Replace("+", string.Empty)
                .Replace("/", string.Empty)
                .Replace("=", string.Empty);
        }

        private static char Pick(string source)
        {
            var index = NextInt(source.Length);
            return source[index];
        }

        private static int NextInt(int maxExclusive)
        {
            var bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var value = BitConverter.ToUInt32(bytes, 0);
            return (int)(value % (uint)maxExclusive);
        }

        private static string Shuffle(string input)
        {
            var chars = input.ToCharArray();
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = NextInt(i + 1);
                var temp = chars[i];
                chars[i] = chars[j];
                chars[j] = temp;
            }

            return new string(chars);
        }
    }
}
