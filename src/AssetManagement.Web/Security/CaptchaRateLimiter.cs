using System;
using System.Web;
using AssetManagement.Application.Contracts.Security;

namespace AssetManagement.Web.Security
{
    public static class CaptchaRateLimiter
    {
        public static bool TryAcquire(HttpContextBase context, string scope, IDistributedRateLimiter limiter)
        {
            if (context == null || context.Request == null)
            {
                return true;
            }

            var address = context.Request.UserHostAddress ?? "unknown";
            var key = "captcha|" + (scope ?? "captcha") + "|" + address;
            return limiter != null && limiter.TryAcquire(key, 20, TimeSpan.FromMinutes(1));
        }
    }
}
