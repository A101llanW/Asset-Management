using Microsoft.AspNetCore.Http;

namespace AssetSuite.Web.Infrastructure;

public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;

    public TenantResolutionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("tenant", out var tenantObj))
        {
            var tenant = tenantObj as string;
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                context.Items["TenantRouteToken"] = tenant;
            }
        }

        await _next(context);
    }
}
