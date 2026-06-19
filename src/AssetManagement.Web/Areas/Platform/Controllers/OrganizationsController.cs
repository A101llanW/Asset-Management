using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.ViewModels;
using AssetManagement.Application.ViewModels.Organizations;
using AssetManagement.Domain.Enums;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;

namespace AssetManagement.Web.Areas.Platform.Controllers
{
    [PermissionAuthorize("Platform.Organizations.View")]
    public class OrganizationsController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;
        private readonly IOrganizationService _organizationService;
        private readonly IOrganizationLicenseService _licenseService;
        private readonly IUserAccountQueryRepository _userAccountQuery;
        private readonly IAuditLogService _auditLogService;

        public OrganizationsController()
        {
            _unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
            _organizationService = DependencyResolver.Current.GetService<IOrganizationService>();
            _licenseService = DependencyResolver.Current.GetService<IOrganizationLicenseService>();
            _userAccountQuery = DependencyResolver.Current.GetService<IUserAccountQueryRepository>();
            _auditLogService = DependencyResolver.Current.GetService<IAuditLogService>();
        }

        public ActionResult Index()
        {
            EnsurePlatformAccess();
            var organizations = _unitOfWork.Repository<Organization>().Query().OrderBy(o => o.Name).ToList();
            var licenseByOrganization = LoadLicenseSummariesByOrganization(organizations.Count);
            var assetCounts = LoadAssetCountsByOrganization();

            var summaries = organizations.Select(o =>
            {
                LicenseListItemVm license;
                licenseByOrganization.TryGetValue(o.Id, out license);
                int assetCount;
                assetCounts.TryGetValue(o.Id, out assetCount);
                return new OrganizationSummaryViewModel
                {
                    Id = o.Id,
                    Name = o.Name,
                    Slug = o.Slug,
                    Status = o.Status,
                    IsActive = o.IsActive,
                    CreatedDate = o.CreatedAt,
                    UserCount = CountUsersForOrganization(o.Id),
                    AssetCount = assetCount,
                    LicenseExpiryDate = license != null ? (DateTime?)license.ExpiryDate : null,
                    DaysUntilExpiry = license != null ? (int?)license.DaysRemaining : null,
                    LicenseEffectiveStatus = license != null ? license.EffectiveStatus : LicenseStatus.Expired
                };
            }).ToList();

            var model = new OrganizationsIndexViewModel
            {
                TotalOrganizations = organizations.Count,
                ActiveOrganizations = organizations.Count(o => o.IsActive),
                ExpiringSoon = summaries.Count(s =>
                    s.LicenseExpiryDate.HasValue
                    && s.DaysUntilExpiry.HasValue
                    && s.DaysUntilExpiry.Value >= 0
                    && s.DaysUntilExpiry.Value <= 30),
                ExpiredLicenses = summaries.Count(s =>
                    s.LicenseEffectiveStatus == LicenseStatus.Expired
                    || (s.DaysUntilExpiry.HasValue && s.DaysUntilExpiry.Value < 0)),
                Organizations = summaries
            };
            return View(model);
        }

        [PermissionAuthorize("Platform.Organizations.Manage")]
        public ActionResult Create()
        {
            EnsurePlatformAccess();
            return View(new CreateOrganizationViewModel());
        }

        public ActionResult OrganizationDetails(int id)
        {
            EnsurePlatformAccess();
            var organization = _unitOfWork.Repository<Organization>().GetById(id);
            if (organization == null)
            {
                return HttpNotFound();
            }

            _organizationScope.SetOrganizationFilterOverride(id);
            try
            {
                var actorName = GetActorName();
                var companyAdmins = _userAccountQuery
                    .GetUsersForOrganization(id, null, true)
                    .Where(u => u.IsActive && string.Equals(u.RoleName, "Company Admin", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var impersonationState = LoadImpersonationState(id, actorName);
                var licenseDetail = _licenseService != null ? _licenseService.GetByOrganizationId(id) : null;
                var allUsers = _userAccountQuery
                    .GetUsersForOrganization(id, null, true)
                    .OrderBy(u => u.LastName)
                    .ThenBy(u => u.FirstName)
                    .ThenBy(u => u.Email)
                    .ToList();
                var recentAuditLogs = _auditLogService != null
                    ? _auditLogService.GetLogs(new AuditLogFilterVm())
                        .OrderByDescending(a => a.Timestamp)
                        .Take(50)
                        .ToList()
                    : new List<AuditLogVm>();
                var model = new OrganizationDetailsViewModel
                {
                    Organization = organization,
                    UserCount = CountUsersForOrganization(id),
                    AssetCount = _unitOfWork.Repository<Asset>().Query().Count(a => a.OrganizationId == id),
                    DepartmentCount = _unitOfWork.Repository<Department>().Query().Count(d => d.OrganizationId == id),
                    CompanyAdmins = companyAdmins,
                    Users = allUsers,
                    RecentAuditLogs = recentAuditLogs,
                    PendingImpersonationRequests = impersonationState.Pending,
                    ActiveApprovedRequest = impersonationState.ActiveApproved,
                    ImpersonationHistory = LoadImpersonationHistory(id),
                    LicenseEffectiveStatus = licenseDetail != null ? licenseDetail.EffectiveStatus : LicenseStatus.Expired,
                    LicenseExpiryDate = licenseDetail != null ? (DateTime?)licenseDetail.ExpiryDate : null,
                    DaysUntilExpiry = licenseDetail != null ? (int?)licenseDetail.DaysRemaining : null,
                    LicenseHistory = licenseDetail != null && licenseDetail.History != null
                        ? licenseDetail.History.ToList()
                        : new List<LicenseHistoryItemVm>()
                };
                return View(model);
            }
            finally
            {
                _organizationScope.SetOrganizationFilterOverride(null);
            }
        }

        [PermissionAuthorize("Platform.Organizations.Manage")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateOrganization(CreateOrganizationViewModel model)
        {
            EnsurePlatformAccess();
            if (model == null || string.IsNullOrWhiteSpace(model.Name))
            {
                ModelState.AddModelError("Name", "Organization name is required.");
                return View("Create", model ?? new CreateOrganizationViewModel());
            }

            var result = _organizationService.CreateOrganization(new OrganizationCreateRequest
            {
                Name = model.Name,
                Slug = model.Slug,
                AdminEmail = model.AdminEmail,
                AdminFirstName = model.AdminFirstName,
                AdminLastName = model.AdminLastName
            });

            TempData[result.Succeeded ? "Message" : "Error"] = result.Succeeded
                ? result.Message + " Portal login: /" + result.Organization.Slug + "/Account/Login"
                : result.Message;
            if (result.Succeeded && result.Organization != null)
            {
                return RedirectToAction("OrganizationDetails", new { id = result.Organization.Id });
            }

            return View("Create", model);
        }

        [PermissionAuthorize("Platform.Support.Impersonate")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RequestImpersonation(int organizationId, string targetAdmin, string reason)
        {
            EnsurePlatformAccess();
            if (string.IsNullOrWhiteSpace(targetAdmin))
            {
                TempData["Error"] = "A target company administrator is required.";
                return RedirectToAction("OrganizationDetails", new { id = organizationId });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A support reason is required.";
                return RedirectToAction("OrganizationDetails", new { id = organizationId });
            }

            var request = new ImpersonationRequest
            {
                OrganizationId = organizationId,
                RequestedBy = GetActorName(),
                RequestedFrom = targetAdmin,
                RequestDate = DateTime.Now,
                Status = ImpersonationRequestStatus.Pending,
                Reason = reason,
                ExpiryDate = DateTime.Now.AddHours(24),
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _unitOfWork.Repository<ImpersonationRequest>().Add(request);
            _unitOfWork.SaveChanges();

            _auditWriter.Write(
                "IMPERSONATION_REQUESTED",
                "Organization",
                organizationId.ToString(),
                null,
                "{\"TargetAdmin\":\"" + targetAdmin + "\"}");

            if (IsAjaxRequest())
            {
                return Json(new
                {
                    success = true,
                    requestId = request.Id,
                    message = "Impersonation request sent to " + targetAdmin + ". Please wait for approval."
                });
            }

            TempData["Message"] = "Impersonation request sent to " + targetAdmin + ". Please wait for approval.";
            return RedirectToAction("OrganizationDetails", new { id = organizationId });
        }

        [PermissionAuthorize("Platform.Support.Impersonate")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelImpersonationRequest(int requestId)
        {
            EnsurePlatformAccess();
            var request = _unitOfWork.Repository<ImpersonationRequest>().GetById(requestId);
            if (request == null || request.RequestedBy != GetActorName())
            {
                return HttpNotFound();
            }

            if (request.Status == ImpersonationRequestStatus.Pending)
            {
                request.Status = ImpersonationRequestStatus.Cancelled;
                request.DecisionDate = DateTime.Now;
                _unitOfWork.Repository<ImpersonationRequest>().Update(request);
                _unitOfWork.SaveChanges();

                _auditWriter.Write("IMPERSONATION_CANCELLED", "Organization", request.OrganizationId.ToString(), null, null);

                if (IsAjaxRequest())
                {
                    return Json(new { success = true, message = "Impersonation request cancelled." });
                }

                TempData["Message"] = "Impersonation request cancelled.";
            }

            if (IsAjaxRequest())
            {
                return Json(new { success = false, message = "Request is no longer pending." });
            }

            return RedirectToAction("OrganizationDetails", new { id = request.OrganizationId });
        }

        [PermissionAuthorize("Platform.Support.Operate")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Elevate(int requestId)
        {
            return HandleElevate(requestId);
        }

        [PermissionAuthorize("Platform.Support.Impersonate")]
        public ActionResult StopImpersonating()
        {
            int? organizationId = Session["ImpersonatedOrganizationId"] as int?;
            var actorName = GetActorName();
            if (ImpersonationSessionHelper.IsSessionImpersonating(Session))
            {
                if (!ImpersonationSessionHelper.TryEndActiveImpersonation(Session, _unitOfWork, _auditWriter, actorName))
                {
                    ImpersonationSessionHelper.TryClearStaleImpersonationSession(Session, _unitOfWork, _auditWriter, actorName);
                }
                TempData["Message"] = "Impersonation session closed.";
            }

            return Redirect(ImpersonationSessionHelper.BuildPlatformAdminPostExpiryUrl(Url, organizationId));
        }

        public JsonResult GetMyImpersonationStatus()
        {
            EnsurePlatformAccess();
            var status = ImpersonationSessionHelper.ResolveMyImpersonationStatus(Session, _unitOfWork, Url);
            return Json(new
            {
                secondsLeft = status.SecondsLeft,
                redirectUrl = status.RedirectUrl
            }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult CheckRequestStatus(int requestId)
        {
            EnsurePlatformAccess();
            var request = _unitOfWork.Repository<ImpersonationRequest>().GetById(requestId);
            if (request == null)
            {
                return Json(new { status = "NotFound" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { status = request.Status.ToString() }, JsonRequestBehavior.AllowGet);
        }

        private ActionResult HandleElevate(int requestId)
        {
            EnsurePlatformAccess();
            var request = _unitOfWork.Repository<ImpersonationRequest>().GetById(requestId);
            var actorName = GetActorName();
            if (request == null || string.IsNullOrEmpty(actorName) || request.RequestedBy != actorName)
            {
                return HttpNotFound();
            }

            if (request.Status != ImpersonationRequestStatus.Approved &&
                request.Status != ImpersonationRequestStatus.Active)
            {
                TempData["Error"] = "This request has not been approved or has expired.";
                return RedirectToAction("OrganizationDetails", new { id = request.OrganizationId });
            }

            if (request.ExpiryDate.HasValue && request.ExpiryDate.Value < DateTime.Now)
            {
                request.Status = ImpersonationRequestStatus.Expired;
                _unitOfWork.Repository<ImpersonationRequest>().Update(request);
                _unitOfWork.SaveChanges();
                TempData["Error"] = "This authorization has expired.";
                return RedirectToAction("OrganizationDetails", new { id = request.OrganizationId });
            }

            var organization = _unitOfWork.Repository<Organization>().GetById(request.OrganizationId.Value);
            if (organization == null)
            {
                return HttpNotFound();
            }

            _auditWriter.Write(
                "IMPERSONATION_START",
                "Organization",
                request.OrganizationId.ToString(),
                null,
                "{\"ApprovedBy\":\"" + request.RequestedFrom + "\"}");

            ImpersonationSessionHelper.ApplySession(Session, request, organization);
            request.Status = ImpersonationRequestStatus.Active;
            _unitOfWork.Repository<ImpersonationRequest>().Update(request);
            _unitOfWork.SaveChanges();

            TempData["Message"] = "Now elevated into " + organization.Name + ".";
            if (string.IsNullOrWhiteSpace(organization.Slug))
            {
                TempData["Error"] = "This organization does not have a portal URL slug configured.";
                return RedirectToAction("OrganizationDetails", new { id = organization.Id });
            }

            return TenantUrlHelper.CreateTenantRedirect(organization.Slug, "Dashboard", "Index");
        }

        private OrganizationImpersonationState LoadImpersonationState(int organizationId, string actorName)
        {
            var now = DateTime.Now;
            var requests = _unitOfWork.Repository<ImpersonationRequest>().Query()
                .Where(r => r.OrganizationId == organizationId && r.RequestedBy == actorName)
                .ToList();

            return new OrganizationImpersonationState
            {
                Pending = requests.Where(r => r.Status == ImpersonationRequestStatus.Pending)
                    .OrderByDescending(r => r.RequestDate).ToList(),
                ActiveApproved = requests.FirstOrDefault(r =>
                    (r.Status == ImpersonationRequestStatus.Approved || r.Status == ImpersonationRequestStatus.Active) &&
                    (!r.ExpiryDate.HasValue || r.ExpiryDate > now)),
            };
        }

        private List<ImpersonationRequest> LoadImpersonationHistory(int organizationId)
        {
            return _unitOfWork.Repository<ImpersonationRequest>().Query()
                .Where(r => r.OrganizationId == organizationId)
                .OrderByDescending(r => r.RequestDate)
                .Take(50)
                .ToList();
        }

        private int CountUsersForOrganization(int organizationId)
        {
            return _userAccountQuery.CountUsersForOrganization(organizationId);
        }

        private Dictionary<int, LicenseListItemVm> LoadLicenseSummariesByOrganization(int organizationCount)
        {
            if (_licenseService == null || organizationCount <= 0)
            {
                return new Dictionary<int, LicenseListItemVm>();
            }

            var pageSize = Math.Max(organizationCount, 100);
            var licenses = _licenseService.GetLicenseListPage(
                new LicenseListFilterVm(),
                "organization",
                "asc",
                1,
                pageSize).Items;

            return licenses
                .GroupBy(l => l.OrganizationId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        private Dictionary<int, int> LoadAssetCountsByOrganization()
        {
            return _unitOfWork.Repository<Asset>().Query()
                .Where(a => a.OrganizationId.HasValue)
                .GroupBy(a => a.OrganizationId.Value)
                .Select(g => new { OrganizationId = g.Key, Count = g.Count() })
                .ToList()
                .ToDictionary(x => x.OrganizationId, x => x.Count);
        }

        private static readonly HashSet<string> ImpersonationSafeActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Index",
            "OrganizationDetails",
            "CheckRequestStatus",
            "GetMyImpersonationStatus",
            "Elevate",
            "RequestImpersonation",
            "CancelImpersonationRequest",
            "StopImpersonating"
        };

        private void EnsurePlatformAccess()
        {
            if (_organizationScope.IsImpersonating())
            {
                var actionName = ControllerContext != null && ControllerContext.RouteData != null
                    ? ControllerContext.RouteData.GetRequiredString("action")
                    : null;
                if (!_organizationScope.IsActualPlatformAdmin() ||
                    string.IsNullOrWhiteSpace(actionName) ||
                    !ImpersonationSafeActions.Contains(actionName))
                {
                    var organizationId = Session["ImpersonatedOrganizationId"] as int?;
                    var slug = organizationId.HasValue
                        ? TenantUrlHelper.ResolveOrganizationSlug(_unitOfWork, organizationId.Value)
                        : null;
                    var target = TenantUrlHelper.TenantRouteUrl(Url, slug, "Index", "Dashboard")
                        ?? Url.Action("Index", "Dashboard", new { area = string.Empty });
                    Response.Redirect(target);
                }

                return;
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

        private bool IsAjaxRequest()
        {
            return Request != null &&
                string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class OrganizationImpersonationState
        {
            public List<ImpersonationRequest> Pending { get; set; }
            public ImpersonationRequest ActiveApproved { get; set; }
        }
    }

    public class OrganizationsIndexViewModel
    {
        public int TotalOrganizations { get; set; }

        public int ActiveOrganizations { get; set; }

        public int ExpiringSoon { get; set; }

        public int ExpiredLicenses { get; set; }

        public List<OrganizationSummaryViewModel> Organizations { get; set; }
    }

    public class OrganizationSummaryViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Slug { get; set; }

        public string Status { get; set; }

        public bool IsActive { get; set; }

        public DateTime CreatedDate { get; set; }

        public int UserCount { get; set; }

        public int AssetCount { get; set; }

        public DateTime? LicenseExpiryDate { get; set; }

        public int? DaysUntilExpiry { get; set; }

        public LicenseStatus LicenseEffectiveStatus { get; set; }
    }

    public class OrganizationDetailsViewModel
    {
        public Organization Organization { get; set; }

        public int UserCount { get; set; }

        public int AssetCount { get; set; }

        public int DepartmentCount { get; set; }

        public List<UserVm> CompanyAdmins { get; set; }

        public List<UserVm> Users { get; set; }

        public List<AuditLogVm> RecentAuditLogs { get; set; }

        public List<ImpersonationRequest> PendingImpersonationRequests { get; set; }

        public ImpersonationRequest ActiveApprovedRequest { get; set; }

        public List<ImpersonationRequest> ImpersonationHistory { get; set; }

        public LicenseStatus LicenseEffectiveStatus { get; set; }

        public DateTime? LicenseExpiryDate { get; set; }

        public int? DaysUntilExpiry { get; set; }

        public List<LicenseHistoryItemVm> LicenseHistory { get; set; }
    }

    public class CreateOrganizationViewModel
    {
        public string Name { get; set; }
        public string Slug { get; set; }
        public string AdminEmail { get; set; }
        public string AdminFirstName { get; set; }
        public string AdminLastName { get; set; }
    }
}
