using System;
using System.Web;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Security
{
    public static class ScanLookupRateLimiter
    {
        public static bool TryAcquire(HttpContextBase context, IDistributedRateLimiter limiter)
        {
            if (context == null || context.Request == null)
            {
                return true;
            }

            var tenant = TenantUrlHelper.GetTenantToken(context);
            var address = context.Request.UserHostAddress ?? "unknown";
            var key = "scan-lookup|" + (string.IsNullOrWhiteSpace(tenant) ? "global" : tenant) + "|" + address;
            return limiter != null && limiter.TryAcquire(key, 30, TimeSpan.FromMinutes(1));
        }
    }
}
