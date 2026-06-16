using System;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.DTOs;
using AssetManagement.Application.ViewModels.Organizations;
using AssetManagement.Domain.Enums;
using AssetManagement.Web.Filters;
using AssetManagement.Web.ViewModels;

namespace AssetManagement.Web.Areas.Platform.Controllers
{
    [PermissionAuthorize("Platform.Licenses.View")]
    public class LicensesController : Controller
    {
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOrganizationLicenseService _licenseService;

        public LicensesController()
        {
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _licenseService = DependencyResolver.Current.GetService<IOrganizationLicenseService>();
        }

        public ActionResult Index(string search, string status, string planCode, int? expiringWithinDays, string sort, string direction, int page = 1, int pageSize = 20)
        {
            EnsurePlatformAccess();
            var pageModel = _licenseService.GetLicenseListPage(
                new LicenseListFilterVm
                {
                    Search = search,
                    Status = status,
                    PlanCode = planCode,
                    ExpiringWithinDays = expiringWithinDays
                },
                sort,
                direction,
                page,
                pageSize);

            var model = new LicenseIndexViewModel
            {
                ListPage = pageModel,
                Items = pageModel.Items,
                Search = search,
                Status = status,
                PlanCode = planCode,
                ExpiringWithinDays = expiringWithinDays,
                Sort = sort ?? "expiry",
                Direction = direction ?? "asc",
                Page = pageModel.Page,
                PageSize = pageModel.PageSize,
                TotalCount = pageModel.TotalCount
            };

            return View(model);
        }

        public ActionResult Details(int orgId)
        {
            EnsurePlatformAccess();
            var detail = _licenseService.GetByOrganizationId(orgId);
            if (detail == null)
            {
                return HttpNotFound();
            }

            return View(detail);
        }

        [PermissionAuthorize("Platform.Licenses.Manage")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Renew(RenewLicenseFormModel model)
        {
            EnsurePlatformAccess();
            return ExecuteLicenseAction(() => _licenseService.Renew(new RenewLicenseRequest
            {
                OrganizationId = model.OrganizationId,
                NewExpiryDate = model.NewExpiryDate,
                Notes = model.Notes
            }, GetActorName()), model.OrganizationId);
        }

        [PermissionAuthorize("Platform.Licenses.Manage")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Pause(PauseLicenseFormModel model)
        {
            EnsurePlatformAccess();
            return ExecuteLicenseAction(() => _licenseService.Pause(new PauseLicenseRequest
            {
                OrganizationId = model.OrganizationId,
                Reason = model.Reason
            }, GetActorName()), model.OrganizationId);
        }

        [PermissionAuthorize("Platform.Licenses.Manage")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Resume(ResumeLicenseFormModel model)
        {
            EnsurePlatformAccess();
            return ExecuteLicenseAction(() => _licenseService.Resume(new ResumeLicenseRequest
            {
                OrganizationId = model.OrganizationId,
                Notes = model.Notes
            }, GetActorName()), model.OrganizationId);
        }

        [PermissionAuthorize("Platform.Licenses.Manage")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdatePlan(UpdatePlanFormModel model)
        {
            EnsurePlatformAccess();
            return ExecuteLicenseAction(() => _licenseService.UpdatePlan(new UpdatePlanRequest
            {
                OrganizationId = model.OrganizationId,
                PlanCode = model.PlanCode,
                PlanName = model.PlanName,
                MaxUsers = model.MaxUsers,
                Notes = model.Notes
            }, GetActorName()), model.OrganizationId);
        }

        private ActionResult ExecuteLicenseAction(Func<LicenseOperationResult> action, int organizationId)
        {
            try
            {
                var result = action();
                TempData[result.Succeeded ? "Message" : "Error"] = result.Message;
            }
            catch (BusinessException ex)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction("Details", new { orgId = organizationId });
        }

        private void EnsurePlatformAccess()
        {
            if (_organizationScope.IsImpersonating())
            {
                throw new UnauthorizedAccessException("Platform license management is not available during impersonation.");
            }

            if (!_organizationScope.IsActualPlatformAdmin())
            {
                throw new UnauthorizedAccessException("Platform access required.");
            }
        }

        private string GetActorName()
        {
            return User != null && User.Identity != null ? User.Identity.Name : "System";
        }
    }

    public class LicenseIndexViewModel : ListPageViewModel<LicenseListItemVm>
    {
        public LicenseListPageVm ListPage { get; set; }

        public string Status { get; set; }

        public string PlanCode { get; set; }

        public int? ExpiringWithinDays { get; set; }
    }

    public class RenewLicenseFormModel
    {
        public int OrganizationId { get; set; }

        public DateTime NewExpiryDate { get; set; }

        public string Notes { get; set; }
    }

    public class PauseLicenseFormModel
    {
        public int OrganizationId { get; set; }

        public string Reason { get; set; }
    }

    public class ResumeLicenseFormModel
    {
        public int OrganizationId { get; set; }

        public string Notes { get; set; }
    }

    public class UpdatePlanFormModel
    {
        public int OrganizationId { get; set; }

        public string PlanCode { get; set; }

        public string PlanName { get; set; }

        public int? MaxUsers { get; set; }

        public string Notes { get; set; }
    }
}
