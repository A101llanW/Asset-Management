using System;
using System.Web;

namespace AssetManagement.Web.Helpers
{
    public static class CaptchaSessionHelper
    {
        public const string CaptchaTextKey = "CaptchaText";
        public const string CaptchaExpiryKey = "CaptchaExpiry";
        public const string CaptchaIdKey = "CaptchaId";

        public static void Store(HttpSessionStateBase session, string text, DateTime expiryUtc, string captchaId)
        {
            if (session == null)
            {
                return;
            }

            session[CaptchaTextKey] = text;
            session[CaptchaExpiryKey] = expiryUtc;
            session[CaptchaIdKey] = captchaId;
        }

        public static void Clear(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return;
            }

            session.Remove(CaptchaTextKey);
            session.Remove(CaptchaExpiryKey);
            session.Remove(CaptchaIdKey);
        }

        public static string ValidateSubmittedCode(HttpSessionStateBase session, string userInput, bool clearOnSuccess)
        {
            if (session == null)
            {
                return "CAPTCHA session expired. Please try again.";
            }

            var sessionText = session[CaptchaTextKey] as string;
            var sessionExpiry = session[CaptchaExpiryKey] as DateTime?;
            var sessionId = session[CaptchaIdKey] as string;

            if (string.IsNullOrEmpty(sessionText) || !sessionExpiry.HasValue || string.IsNullOrEmpty(sessionId))
            {
                return "CAPTCHA session expired. Please try again.";
            }

            if (DateTime.UtcNow > sessionExpiry.Value)
            {
                Clear(session);
                return "CAPTCHA expired. Please try again.";
            }

            if (string.IsNullOrWhiteSpace(userInput)
                || !string.Equals(userInput.Trim(), sessionText, StringComparison.OrdinalIgnoreCase))
            {
                return "Invalid security code. Please try again.";
            }

            if (clearOnSuccess)
            {
                Clear(session);
            }

            return null;
        }
    }
}
