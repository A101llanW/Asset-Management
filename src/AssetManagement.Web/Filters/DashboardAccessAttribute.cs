using System;
using AssetManagement.Application.Security;

namespace AssetManagement.Web.Filters
{
    /// <summary>
    /// Dashboard / Notifications gate: any working tenant right (ModulePermissionCatalog.DashboardAccessPermissionCodes),
    /// not only Reports.View. Data on those pages is still department-scoped.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class DashboardAccessAttribute : AnyPermissionAuthorizeAttribute
    {
        public DashboardAccessAttribute()
            : base(ModulePermissionCatalog.DashboardAccessPermissionCodes)
        {
        }
    }
}
