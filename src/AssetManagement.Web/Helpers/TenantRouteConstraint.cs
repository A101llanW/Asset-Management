using System;
using System.Web;
using System.Web.Routing;
using AssetManagement.Application.Helpers;

namespace AssetManagement.Web.Helpers
{
    public class TenantRouteConstraint : IRouteConstraint
    {
        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (values == null || !values.ContainsKey(parameterName))
            {
                return false;
            }

            var token = values[parameterName] as string;
            return IsPlausibleTenantToken(token);
        }

        public static bool IsPlausibleTenantToken(string token)
        {
            return TenantTokenValidator.IsPlausibleToken(token, TenantReservedSegments.IsReserved);
        }
    }
}
