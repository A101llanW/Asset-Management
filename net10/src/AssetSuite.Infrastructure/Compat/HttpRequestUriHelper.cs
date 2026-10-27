using System;
using Microsoft.AspNetCore.Http;

namespace AssetManagement.Infrastructure.Compat
{
    public static class HttpRequestUriHelper
    {
        public static Uri GetRequestUri(HttpContext context)
        {
            if (context?.Request == null)
            {
                return null;
            }

            var request = context.Request;
            return new Uri(request.Scheme + "://" + request.Host + request.PathBase);
        }
    }
}
