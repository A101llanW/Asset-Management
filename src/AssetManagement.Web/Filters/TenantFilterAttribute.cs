using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Filters
{
    public class TenantFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (filterContext == null)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var httpContext = filterContext.HttpContext;
            var routeData = filterContext.RouteData;
            var areaName = routeData.DataTokens.ContainsKey("area") ? routeData.DataTokens["area"] as string : null;

            if (string.Equals(areaName, "Platform", StringComparison.OrdinalIgnoreCase))
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var tenantToken = TenantUrlHelper.GetTenantToken(routeData);
            ResolveTenantContext(filterContext, tenantToken);

            if (httpContext == null || httpContext.User == null || !httpContext.User.Identity.IsAuthenticated)
            {
                base.OnActionExecuting(filterContext);
                return;
            }

            var licenseBlock = BuildLicenseBlock(filterContext);
            if (licenseBlock != null)
            {
                filterContext.Result = licenseBlock;
                return;
            }

            var redirect = BuildTenantRedirect(filterContext, tenantToken);
            if (redirect != null)
            {
                filterContext.Result = redirect;
                return;
            }

            base.OnActionExecuting(filterContext);
        }

        private static void ResolveTenantContext(ActionExecutingContext filterContext, string tenantToken)
        {
            if (string.IsNullOrWhiteSpace(tenantToken))
            {
                return;
            }

            var unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            if (unitOfWork == null)
            {
                return;
            }

            var organization = unitOfWork.Repository<Organization>().Query()
                .FirstOrDefault(o => o.IsActive
                    && o.Slug != null
                    && o.Slug.Equals(tenantToken, StringComparison.OrdinalIgnoreCase));

            if (organization == null)
            {
                return;
            }

            filterContext.HttpContext.Items[TenantContextKeys.OrganizationId] = organization.Id;
            filterContext.HttpContext.Items[TenantContextKeys.TenantToken] = tenantToken;
            filterContext.Controller.ViewBag.TenantContext = organization;
            filterContext.Controller.ViewBag.TenantToken = tenantToken;
            filterContext.Controller.ViewBag.IsTenantPortal = true;
        }

        private static ActionResult BuildLicenseBlock(ActionExecutingContext filterContext)
        {
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var actionName = filterContext.ActionDescriptor.ActionName;

            if (IsLicenseWhitelisted(controllerName, actionName))
            {
                return null;
            }

            var organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            if (organizationScope == null)
            {
                return null;
            }

            if (organizationScope.IsImpersonating())
            {
                return null;
            }

            if (organizationScope.IsActualPlatformAdmin())
            {
                return null;
            }

            var organizationId = ResolveOrganizationIdForLicense(filterContext, organizationScope);
            if (!organizationId.HasValue)
            {
                return null;
            }

            var licenseService = DependencyResolver.Current.GetService<IOrganizationLicenseService>();
            if (licenseService == null)
            {
                return null;
            }

            var license = licenseService.GetLicenseForOrganization(organizationId.Value);
            var effectiveStatus = licenseService.GetEffectiveStatus(license);
            if (effectiveStatus == LicenseStatus.Active || effectiveStatus == LicenseStatus.PendingRenewal)
            {
                return null;
            }

            var tenantSlug = TenantUrlHelper.GetTenantToken(filterContext.RouteData);
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                var unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
                tenantSlug = TenantUrlHelper.ResolveOrganizationSlug(unitOfWork, organizationId.Value);
            }

            if (IsApiRequest(filterContext))
            {
                filterContext.HttpContext.Response.StatusCode = 403;
                filterContext.HttpContext.Response.TrySkipIisCustomErrors = true;
                return new JsonResult
                {
                    Data = new { error = effectiveStatus == LicenseStatus.Paused ? "license_suspended" : "license_expired" },
                    JsonRequestBehavior = JsonRequestBehavior.AllowGet
                };
            }

            var targetAction = effectiveStatus == LicenseStatus.Paused ? "LicenseSuspended" : "LicenseExpired";
            return TenantUrlHelper.CreateTenantRedirect(tenantSlug, "Account", targetAction);
        }

        private static int? ResolveOrganizationIdForLicense(ActionExecutingContext filterContext, IOrganizationScopeService organizationScope)
        {
            var contextOrgId = filterContext.HttpContext.Items[TenantContextKeys.OrganizationId] as int?;
            if (contextOrgId.HasValue)
            {
                return contextOrgId;
            }

            var scopedOrgId = organizationScope.GetCurrentOrganizationId();
            if (scopedOrgId.HasValue)
            {
                return scopedOrgId;
            }

            var userId = filterContext.HttpContext.User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            if (unitOfWork == null)
            {
                return null;
            }

            var user = unitOfWork.Repository<ApplicationUser>().GetById(userId);
            return user != null ? user.OrganizationId : null;
        }

        private static bool IsApiRequest(ActionExecutingContext filterContext)
        {
            var path = filterContext.HttpContext.Request.AppRelativeCurrentExecutionFilePath ?? string.Empty;
            return path.IndexOf("/api/v1/", StringComparison.OrdinalIgnoreCase) >= 0
                || path.StartsWith("~/api/v1/", StringComparison.OrdinalIgnoreCase);
        }

        private static ActionResult BuildTenantRedirect(ActionExecutingContext filterContext, string tenantToken)
        {
            var controllerName = filterContext.ActionDescriptor.ControllerDescriptor.ControllerName;
            var actionName = filterContext.ActionDescriptor.ActionName;

            if (IsWhitelisted(controllerName, actionName))
            {
                return null;
            }

            var organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            var unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            if (organizationScope == null || unitOfWork == null)
            {
                return null;
            }

            string requiredSlug = null;

            if (organizationScope.IsImpersonating())
            {
                var impersonatedId = filterContext.HttpContext.Session["ImpersonatedOrganizationId"] as int?;
                if (impersonatedId.HasValue)
                {
                    requiredSlug = TenantUrlHelper.ResolveOrganizationSlug(unitOfWork, impersonatedId.Value);
                }
            }
            else if (organizationScope.IsActualPlatformAdmin())
            {
                if (string.IsNullOrWhiteSpace(tenantToken))
                {
                    return null;
                }

                var auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
                if (auditWriter != null)
                {
                    auditWriter.Write(
                        "Platform.TenantBrowseBlocked",
                        "Organization",
                        tenantToken,
                        filterContext.HttpContext.User.GetUserId(),
                        "Platform admin attempted tenant portal access without impersonation.");
                }

                return PlatformAdminHelper.CreateOrganizationsRedirect();
            }
            else
            {
                var userId = filterContext.HttpContext.User.GetUserId();
                requiredSlug = TenantUrlHelper.ResolveOrganizationSlug(unitOfWork, userId);
            }

            if (string.IsNullOrWhiteSpace(requiredSlug))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(tenantToken) &&
                string.Equals(tenantToken, requiredSlug, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            object id = null;
            if (filterContext.RouteData.Values.ContainsKey("id"))
            {
                id = filterContext.RouteData.Values["id"];
            }

            return TenantUrlHelper.CreateTenantRedirect(requiredSlug, controllerName, actionName, id);
        }

        private static bool IsWhitelisted(string controllerName, string actionName)
        {
            if (string.Equals(controllerName, "Account", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "Login", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "LogOff", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "Register", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "ForgotPassword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "ResetPassword", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "ConfirmLegalConsent", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "SetupMfa", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "VerifyMfa", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "SendSetupMfaCode", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "ResendMfaCode", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "LicenseSuspended", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "LicenseExpired", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(controllerName, "Home", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "Privacy", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "Terms", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(controllerName, "Captcha", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "Generate", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "Validate", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "Refresh", StringComparison.OrdinalIgnoreCase);
            }

            if (string.Equals(controllerName, "Dashboard", StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(actionName, "GetImpersonationStatus", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "GetMyImpersonationStatus", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "GetPendingRequests", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actionName, "CheckRequestStatus", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static bool IsLicenseWhitelisted(string controllerName, string actionName)
        {
            return IsWhitelisted(controllerName, actionName);
        }
    }
}
