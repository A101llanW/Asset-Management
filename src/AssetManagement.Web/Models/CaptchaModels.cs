using System;

namespace AssetManagement.Web.Models
{
    public class CaptchaResponse
    {
        public string CaptchaId { get; set; }

        public string CaptchaText { get; set; }

        public string CaptchaBase64 { get; set; }

        public DateTime ExpiresAt { get; set; }
    }
}
