using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace AssetManagement.Web.Validation
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class LoginIdentityAttribute : ValidationAttribute
    {
        private static readonly Regex EmailPattern = new Regex(
            @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Regex LocalIdentityPattern = new Regex(
            @"^[\w.-]+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public LoginIdentityAttribute()
            : base("Invalid email address.")
        {
        }

        public override bool IsValid(object value)
        {
            var text = value as string;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var normalized = text.Trim();
            return EmailPattern.IsMatch(normalized) || LocalIdentityPattern.IsMatch(normalized);
        }
    }
}
