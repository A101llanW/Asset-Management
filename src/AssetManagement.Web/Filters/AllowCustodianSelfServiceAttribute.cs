using System;
using System.Web.Mvc;

namespace AssetManagement.Web.Filters
{
    /// <summary>
    /// Marks an action that any signed-in tenant user may reach without the controller's permission / module
    /// attributes (PermissionAuthorize, AnyPermissionAuthorize, ModuleAccess). The action itself must limit what
    /// it returns to the user's own custody in the current organization (e.g. My Assets, Details of an asset the
    /// user holds). Anonymous users are still sent to login.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AllowCustodianSelfServiceAttribute : Attribute
    {
        public static bool AppliesToAuthenticated(AuthorizationContext filterContext)
        {
            return filterContext != null
                && filterContext.ActionDescriptor != null
                && filterContext.HttpContext != null
                && filterContext.HttpContext.User != null
                && filterContext.HttpContext.User.Identity != null
                && filterContext.HttpContext.User.Identity.IsAuthenticated
                && filterContext.ActionDescriptor.IsDefined(typeof(AllowCustodianSelfServiceAttribute), true);
        }
    }
}
