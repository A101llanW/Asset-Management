using System;
using System.Security.Cryptography;

namespace AssetManagement.Infrastructure.Security
{
    /// <summary>
    /// PBKDF2 hasher compatible with ASP.NET Identity v2 seed hashes.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int SubkeyLength = 32;
        private const int Iterations = 1000;

        public static string HashPassword(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }

            byte[] salt;
            byte[] subkey;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, SaltSize, Iterations))
            {
                salt = deriveBytes.Salt;
                subkey = deriveBytes.GetBytes(SubkeyLength);
            }

            var output = new byte[1 + SaltSize + SubkeyLength];
            output[0] = 0;
            Buffer.BlockCopy(salt, 0, output, 1, SaltSize);
            Buffer.BlockCopy(subkey, 0, output, 1 + SaltSize, SubkeyLength);
            return Convert.ToBase64String(output);
        }

        public static bool VerifyHashedPassword(string hashedPassword, string password)
        {
            if (string.IsNullOrEmpty(hashedPassword) || password == null)
            {
                return false;
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(hashedPassword);
            }
            catch (FormatException)
            {
                return false;
            }

            if (decoded.Length != 1 + SaltSize + SubkeyLength || decoded[0] != 0)
            {
                return false;
            }

            var salt = new byte[SaltSize];
            Buffer.BlockCopy(decoded, 1, salt, 0, SaltSize);

            byte[] generatedSubkey;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                generatedSubkey = deriveBytes.GetBytes(SubkeyLength);
            }

            var storedSubkey = new byte[SubkeyLength];
            Buffer.BlockCopy(decoded, 1 + SaltSize, storedSubkey, 0, SubkeyLength);
            return ByteArraysEqual(storedSubkey, generatedSubkey);
        }

        private static bool ByteArraysEqual(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var diff = 0;
            for (var i = 0; i < left.Length; i++)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }
    }
}
