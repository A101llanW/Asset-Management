using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AssetManagement.Application.Security
{
    public static class PasswordPolicy
    {
        public const int MinLength = 8;
        public const int MaxLength = 128;

        public static IEnumerable<string> Validate(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < MinLength)
            {
                yield return "Passwords must be at least " + MinLength + " characters.";
            }

            if (password != null && password.Length > MaxLength)
            {
                yield return "Passwords must be at most " + MaxLength + " characters.";
            }

            if (password != null && !Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
            {
                yield return "Passwords must have at least one non letter or digit character.";
            }

            if (password != null && !Regex.IsMatch(password, @"\d"))
            {
                yield return "Passwords must have at least one digit ('0'-'9').";
            }

            if (password != null && !Regex.IsMatch(password, @"[a-z]"))
            {
                yield return "Passwords must have at least one lowercase ('a'-'z').";
            }

            if (password != null && !Regex.IsMatch(password, @"[A-Z]"))
            {
                yield return "Passwords must have at least one uppercase ('A'-'Z').";
            }

            if (password != null && ContainsSequentialChars(password))
            {
                yield return "Passwords cannot contain sequential characters (e.g. abc, 123).";
            }
        }

        public static string GetPolicyMessage()
        {
            return "Password must be at least " + MinLength + " characters and include uppercase, lowercase, a digit, and a special character. Sequential characters are not allowed.";
        }

        private static bool ContainsSequentialChars(string password)
        {
            var lowerPassword = password.ToLowerInvariant();

            for (var i = 0; i < lowerPassword.Length - 2; i++)
            {
                if (char.IsDigit(lowerPassword[i])
                    && char.IsDigit(lowerPassword[i + 1])
                    && char.IsDigit(lowerPassword[i + 2]))
                {
                    var first = lowerPassword[i] - '0';
                    var second = lowerPassword[i + 1] - '0';
                    var third = lowerPassword[i + 2] - '0';
                    if (second == first + 1 && third == second + 1)
                    {
                        return true;
                    }

                    if (second == first - 1 && third == second - 1)
                    {
                        return true;
                    }
                }
            }

            for (var i = 0; i < lowerPassword.Length - 2; i++)
            {
                if (char.IsLetter(lowerPassword[i])
                    && char.IsLetter(lowerPassword[i + 1])
                    && char.IsLetter(lowerPassword[i + 2]))
                {
                    var first = lowerPassword[i] - 'a';
                    var second = lowerPassword[i + 1] - 'a';
                    var third = lowerPassword[i + 2] - 'a';
                    if (second == first + 1 && third == second + 1)
                    {
                        return true;
                    }

                    if (second == first - 1 && third == second - 1)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
