using System;

using System.Collections.Generic;

using System.Web;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Security

{

    public static class ScanLookupRateLimiter

    {

        private const int MaxRequestsPerWindow = 30;

        private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

        private static readonly object SyncRoot = new object();

        private static readonly Dictionary<string, RateLimitBucket> Buckets = new Dictionary<string, RateLimitBucket>(StringComparer.OrdinalIgnoreCase);



        public static bool TryAcquire(HttpContextBase context)

        {

            if (context == null || context.Request == null)

            {

                return true;

            }



            var tenant = TenantUrlHelper.GetTenantToken(context);
            var address = context.Request.UserHostAddress ?? "unknown";
            var key = string.IsNullOrWhiteSpace(tenant) ? address : tenant + "|" + address;

            var now = DateTime.UtcNow;



            lock (SyncRoot)

            {

                RateLimitBucket bucket;

                if (!Buckets.TryGetValue(key, out bucket) || now - bucket.WindowStart >= Window)

                {

                    bucket = new RateLimitBucket { WindowStart = now, Count = 0 };

                    Buckets[key] = bucket;

                }



                if (bucket.Count >= MaxRequestsPerWindow)

                {

                    return false;

                }



                bucket.Count++;

                return true;

            }

        }



        private sealed class RateLimitBucket

        {

            public DateTime WindowStart { get; set; }



            public int Count { get; set; }

        }

    }

}


