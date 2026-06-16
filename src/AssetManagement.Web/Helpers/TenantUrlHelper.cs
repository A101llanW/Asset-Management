using System;

using System.Globalization;

using System.Web;

using System.Web.Mvc;

using System.Web.Routing;

using AssetManagement.Application.Contracts;

using AssetManagement.Domain.Entities;

using AssetManagement.Infrastructure.Identity;



namespace AssetManagement.Web.Helpers

{

    public static class TenantUrlHelper

    {

        private static readonly string[] ReservedTenantSegments =

        {

            "Account", "api", "Content", "Scripts", "bundles", "Platform", "favicon.ico"

        };



        public static string GetTenantToken(RouteData routeData)

        {

            return routeData != null ? routeData.Values["tenant"] as string : null;

        }



        public static string GetTenantToken(HttpContextBase httpContext)

        {

            if (httpContext == null)

            {

                return null;

            }



            var fromRoute = GetTenantToken(httpContext.Request != null ? httpContext.Request.RequestContext.RouteData : null);

            if (!string.IsNullOrWhiteSpace(fromRoute))

            {

                return fromRoute;

            }



            return httpContext.Items[TenantContextKeys.TenantToken] as string;

        }



        public static string ResolveOrganizationSlug(IUnitOfWork unitOfWork, int organizationId)

        {

            if (unitOfWork == null || organizationId <= 0)

            {

                return null;

            }



            var organization = unitOfWork.Repository<Organization>().GetById(organizationId);

            return organization != null ? organization.Slug : null;

        }



        public static string ResolveOrganizationSlug(IUnitOfWork unitOfWork, string userId)

        {

            if (unitOfWork == null || string.IsNullOrWhiteSpace(userId))

            {

                return null;

            }



            var user = unitOfWork.Repository<ApplicationUser>().GetById(userId);

            if (user == null || !user.OrganizationId.HasValue)

            {

                return null;

            }



            return ResolveOrganizationSlug(unitOfWork, user.OrganizationId.Value);

        }



        public static bool IsValidTenantSlug(string tenantSlug)

        {

            if (string.IsNullOrWhiteSpace(tenantSlug))

            {

                return false;

            }



            var token = tenantSlug.Trim();

            foreach (var reserved in ReservedTenantSegments)

            {

                if (string.Equals(token, reserved, StringComparison.OrdinalIgnoreCase))

                {

                    return false;

                }

            }



            var normalized = token.ToLowerInvariant();

            foreach (var ch in normalized)

            {

                if (!((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-'))

                {

                    return false;

                }

            }



            return normalized.Length > 0 && (normalized[0] < '0' || normalized[0] > '9');

        }



        public static string NormalizeTenantSlug(string tenantSlug)

        {

            return string.IsNullOrWhiteSpace(tenantSlug) ? null : tenantSlug.Trim().ToLowerInvariant();

        }



        public static string BuildTenantPath(string tenantSlug, string controller, string action, object id = null)

        {

            var normalized = NormalizeTenantSlug(tenantSlug);

            var path = "/" + normalized + "/" + controller + "/" + action;

            string idSegment;
            if (TryFormatRouteId(id, out idSegment))

            {

                path += "/" + idSegment;

            }



            return path;

        }



        public static string BuildTenantLoginPath(string tenantSlug, string returnUrl = null)

        {

            var path = BuildTenantPath(tenantSlug, "Account", "Login");

            if (!string.IsNullOrWhiteSpace(returnUrl))

            {

                path += "?returnUrl=" + HttpUtility.UrlEncode(returnUrl);

            }



            return path;

        }



        public static string TenantRouteUrl(UrlHelper url, string action, string controller, object routeValues = null)

        {

            if (url == null)

            {

                return null;

            }



            var tenant = GetTenantToken(url.RequestContext.HttpContext);

            if (IsValidTenantSlug(tenant))

            {

                object id = null;

                if (routeValues != null)

                {

                    var values = new RouteValueDictionary(routeValues);

                    if (values.ContainsKey("id"))

                    {

                        id = values["id"];

                    }

                }



                return BuildTenantPath(tenant, controller, action, id);

            }



            var defaultValues = new RouteValueDictionary(routeValues ?? new { });

            defaultValues["controller"] = controller;

            defaultValues["action"] = action;

            return url.RouteUrl("Default", defaultValues);

        }



        public static string TenantRouteUrl(UrlHelper url, string organizationSlug, string action, string controller, object routeValues = null)

        {

            if (url == null)

            {

                return null;

            }



            if (!IsValidTenantSlug(organizationSlug))

            {

                var defaultValues = new RouteValueDictionary(routeValues ?? new { });

                defaultValues["controller"] = controller;

                defaultValues["action"] = action;

                return url.RouteUrl("Default", defaultValues);

            }



            object id = null;

            if (routeValues != null)

            {

                var values = new RouteValueDictionary(routeValues);

                if (values.ContainsKey("id"))

                {

                    id = values["id"];

                }

            }



            return BuildTenantPath(organizationSlug, controller, action, id);

        }



        public static RouteValueDictionary CreateTenantRouteValues(

            string tenantSlug,

            string controller,

            string action,

            object id = null)

        {

            var values = new RouteValueDictionary();

            values["tenant"] = NormalizeTenantSlug(tenantSlug);

            values["controller"] = controller;

            values["action"] = action;



            string idSegment;
            if (TryFormatRouteId(id, out idSegment))

            {

                values["id"] = idSegment;

            }



            return values;

        }



        public static ActionResult CreateTenantRedirect(

            string tenantSlug,

            string controller,

            string action,

            object id = null)

        {

            if (!IsValidTenantSlug(tenantSlug))

            {

                return new RedirectResult("/Account/Login");

            }



            return new RedirectResult(BuildTenantPath(tenantSlug, controller, action, id));

        }



        public static ActionResult CreateTenantLoginRedirect(string tenantSlug, string returnUrl = null)

        {

            if (!IsValidTenantSlug(tenantSlug))

            {

                if (string.IsNullOrWhiteSpace(returnUrl))

                {

                    return new RedirectResult("/Account/Login");

                }



                return new RedirectResult("/Account/Login?returnUrl=" + HttpUtility.UrlEncode(returnUrl));

            }



            return new RedirectResult(BuildTenantLoginPath(tenantSlug, returnUrl));

        }



        private static bool TryFormatRouteId(object id, out string idSegment)

        {

            idSegment = null;

            if (id == null || ReferenceEquals(id, UrlParameter.Optional))

            {

                return false;

            }



            idSegment = Convert.ToString(id, CultureInfo.InvariantCulture);

            return !string.IsNullOrWhiteSpace(idSegment);

        }

    }

}


