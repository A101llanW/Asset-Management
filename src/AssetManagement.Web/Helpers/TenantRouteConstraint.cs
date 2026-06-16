using System;

using System.Text.RegularExpressions;

using System.Web;

using System.Web.Mvc;

using System.Web.Routing;

using AssetManagement.Infrastructure.Identity;

using AssetManagement.Infrastructure.Persistence;



namespace AssetManagement.Web.Helpers

{

    public class TenantRouteConstraint : IRouteConstraint

    {

        private static readonly Regex SlugPattern = new Regex(@"^[a-z0-9][a-z0-9-]*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);



        private static readonly string[] ReservedSegments =

        {

            "Account", "api", "Content", "Scripts", "bundles", "Platform", "favicon.ico"

        };



        public bool Match(HttpContextBase httpContext, Route route, string parameterName, RouteValueDictionary values, RouteDirection routeDirection)

        {

            if (values == null || !values.ContainsKey(parameterName))

            {

                return false;

            }



            var token = values[parameterName] as string;

            if (string.IsNullOrWhiteSpace(token) || IsReservedSegment(token))

            {

                return false;

            }



            var normalized = token.Trim().ToLowerInvariant();

            if (!SlugPattern.IsMatch(normalized))

            {

                return false;

            }



            if (routeDirection == RouteDirection.UrlGeneration)

            {

                return true;

            }



            var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();

            if (connectionFactory == null)

            {

                return false;

            }



            var users = new UserAccountRepository(connectionFactory);

            return users.FindOrganizationIdBySlug(normalized).HasValue;

        }



        private static bool IsReservedSegment(string token)

        {

            foreach (var reserved in ReservedSegments)

            {

                if (string.Equals(token, reserved, StringComparison.OrdinalIgnoreCase))

                {

                    return true;

                }

            }



            return false;

        }

    }

}


