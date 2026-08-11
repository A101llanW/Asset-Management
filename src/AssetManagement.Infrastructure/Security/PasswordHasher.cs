using System;
using System.Security.Cryptography;

namespace AssetManagement.Infrastructure.Security
{
    /// <summary>
    /// Versioned PBKDF2 hasher. Version 0 matches legacy ASP.NET Identity v2 seed hashes (1000 iterations).
    /// Version 1 uses OWASP-recommended iteration count for PBKDF2-HMAC-SHA1 on .NET Framework 4.x.
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int SubkeyLength = 32;

        /// <summary>Legacy format byte; PBKDF2 with 1,000 iterations.</summary>
        public const byte VersionLegacy = 0;

        /// <summary>Current format byte; PBKDF2 with 100,000 iterations.</summary>
        public const byte VersionCurrent = 1;

        private const int IterationsLegacy = 1000;
        private const int IterationsCurrent = 100000;

        public const string LegacySeedHashBase64 = "ALJwzw5r970vW+fpNg4Ivw5nutwiP9Omge0gCdgtDVM2h6NFmycZ2GwSH5fyBqDTaw==";

        public static string HashPassword(string password)
        {
            return HashPassword(password, VersionCurrent, IterationsCurrent);
        }

        public static PasswordVerificationResult VerifyHashedPassword(string hashedPassword, string password)
        {
            if (string.IsNullOrEmpty(hashedPassword) || password == null)
            {
                return PasswordVerificationResult.Failed;
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(hashedPassword);
            }
            catch (FormatException)
            {
                return PasswordVerificationResult.Failed;
            }

            if (decoded.Length != 1 + SaltSize + SubkeyLength)
            {
                return PasswordVerificationResult.Failed;
            }

            var version = decoded[0];
            int iterations;
            if (version == VersionLegacy)
            {
                iterations = IterationsLegacy;
            }
            else if (version == VersionCurrent)
            {
                iterations = IterationsCurrent;
            }
            else
            {
                return PasswordVerificationResult.Failed;
            }

            var salt = new byte[SaltSize];
            Buffer.BlockCopy(decoded, 1, salt, 0, SaltSize);

            byte[] generatedSubkey;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, salt, iterations))
            {
                generatedSubkey = deriveBytes.GetBytes(SubkeyLength);
            }

            var storedSubkey = new byte[SubkeyLength];
            Buffer.BlockCopy(decoded, 1 + SaltSize, storedSubkey, 0, SubkeyLength);

            if (!ByteArraysEqual(storedSubkey, generatedSubkey))
            {
                return PasswordVerificationResult.Failed;
            }

            return version == VersionCurrent
                ? PasswordVerificationResult.Success
                : PasswordVerificationResult.SuccessRehashNeeded;
        }

        internal static string HashPasswordForVersion(string password, byte version)
        {
            if (version == VersionLegacy)
            {
                return HashPassword(password, VersionLegacy, IterationsLegacy);
            }

            if (version == VersionCurrent)
            {
                return HashPassword(password, VersionCurrent, IterationsCurrent);
            }

            throw new ArgumentOutOfRangeException("version");
        }

        private static string HashPassword(string password, byte version, int iterations)
        {
            if (password == null)
            {
                throw new ArgumentNullException("password");
            }

            byte[] salt;
            byte[] subkey;
            using (var deriveBytes = new Rfc2898DeriveBytes(password, SaltSize, iterations))
            {
                salt = deriveBytes.Salt;
                subkey = deriveBytes.GetBytes(SubkeyLength);
            }

            var output = new byte[1 + SaltSize + SubkeyLength];
            output[0] = version;
            Buffer.BlockCopy(salt, 0, output, 1, SaltSize);
            Buffer.BlockCopy(subkey, 0, output, 1 + SaltSize, SubkeyLength);
            return Convert.ToBase64String(output);
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
