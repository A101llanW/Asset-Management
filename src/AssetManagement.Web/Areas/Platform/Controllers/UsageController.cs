using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts.Platform;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Web.Filters;

namespace AssetManagement.Web.Areas.Platform.Controllers
{
    [PermissionAuthorize("Platform.Organizations.View")]
    public class UsageController : Controller
    {
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IPlatformUsageService _usageService;

        public UsageController()
        {
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _usageService = DependencyResolver.Current.GetService<IPlatformUsageService>();
        }

        public ActionResult Index(bool refresh = false)
        {
            EnsurePlatformAccess();
            var model = _usageService == null
                ? new Application.ViewModels.Platform.PlatformUsagePageVm
                {
                    Rows = new System.Collections.Generic.List<Application.ViewModels.Platform.PlatformUsageOrgRowVm>(),
                    Totals = new Application.ViewModels.Platform.PlatformUsageOrgRowVm { Name = "Total" },
                    LoginActionCounted = Application.Helpers.PlatformUsageMetrics.LoginActionCounted,
                    LoginActionsExcludedNote = Application.Helpers.PlatformUsageMetrics.LoginActionsExcludedNote,
                    GeneratedAtUtc = DateTime.UtcNow
                }
                : _usageService.GetUsage(bypassCache: refresh);
            return View(model);
        }

        private void EnsurePlatformAccess()
        {
            if (_organizationScope != null && _organizationScope.IsImpersonating())
            {
                throw new UnauthorizedAccessException("Platform usage is not available during impersonation.");
            }

            if (_organizationScope == null || !_organizationScope.IsActualPlatformAdmin())
            {
                throw new UnauthorizedAccessException("Platform access required.");
            }
        }
    }
}