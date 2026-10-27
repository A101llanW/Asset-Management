using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace AssetManagement.Infrastructure.Compat
{
    /// <summary>
    /// Bridges legacy organization-scope code that expected System.Web.HttpContext.Current.
    /// </summary>
    public sealed class AspNetCoreHttpBridge
    {
        private readonly IHttpContextAccessor _accessor;

        public AspNetCoreHttpBridge(IHttpContextAccessor accessor)
        {
            _accessor = accessor;
        }

        public static HttpContext Current => StaticAccessor?.HttpContext;

        private static IHttpContextAccessor StaticAccessor { get; set; }

        public static void Configure(IHttpContextAccessor accessor)
        {
            StaticAccessor = accessor;
        }

        public IDictionary<object, object> Items =>
            _accessor.HttpContext?.Items;

        public ISession Session => _accessor.HttpContext?.Session;

        public HttpRequest Request => _accessor.HttpContext?.Request;
    }
}
