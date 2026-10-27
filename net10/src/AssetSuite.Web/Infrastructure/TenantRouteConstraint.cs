using AssetManagement.Application.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AssetSuite.Web.Infrastructure;

public sealed class TenantRouteConstraint : IRouteConstraint
{
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (values == null || !values.TryGetValue(routeKey, out var tokenObj))
        {
            return false;
        }

        var token = tokenObj as string;
        return TenantTokenValidator.IsPlausibleToken(token, TenantReservedSegments.IsReserved);
    }
}
