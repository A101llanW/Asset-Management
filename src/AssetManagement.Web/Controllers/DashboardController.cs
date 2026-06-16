using System;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    [PermissionAuthorize("Reports.View")]
    public class DashboardController : BaseController
    {
        private readonly IReportService _reportService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IAuditWriter _auditWriter;

        public DashboardController()
        {
            _reportService = BuildReportService();
            _unitOfWork = UnitOfWork;
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _auditWriter = AuditWriter;
        }

        public ActionResult Index()
        {
            if (_organizationScope != null && _organizationScope.IsActualPlatformAdmin() && !_organizationScope.IsImpersonating())
            {
                return PlatformAdminHelper.CreateOrganizationsRedirect();
            }

            var model = _reportService.GetDashboard();
            return View(model);
        }

        public JsonResult GetPendingRequests()
        {
            if (!IsCurrentUserCompanyAdmin())
            {
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
            }

            var user = GetImpersonationScopeUser();
            if (user == null || !user.OrganizationId.HasValue)
            {
                return Json(new { count = 0 }, JsonRequestBehavior.AllowGet);
            }

            var identityName = User.Identity.Name;
            var requests = _unitOfWork.Repository<ImpersonationRequest>().Query()
                .Where(r => r.OrganizationId == user.OrganizationId.Value &&
                    r.Status == ImpersonationRequestStatus.Pending)
                .ToList()
                .Where(r => string.Equals(r.RequestedFrom, identityName, StringComparison.OrdinalIgnoreCase))
                .Select(r => new
                {
                    id = r.Id,
                    requestedBy = r.RequestedBy,
                    requestDate = r.RequestDate.ToString("HH:mm dd MMM yyyy"),
                    reason = r.Reason
                })
                .ToList();

            return Json(new { count = requests.Count, requests = requests }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetImpersonationStatus()
        {
            var user = GetImpersonationScopeUser();
            if (user == null || !user.OrganizationId.HasValue)
            {
                return Json(new { isLocked = false }, JsonRequestBehavior.AllowGet);
            }

            ImpersonationSessionHelper.ExpireStaleImpersonationRequestsForOrganization(user.OrganizationId.Value, _unitOfWork);
            var activeImpersonation = GetActiveImpersonationRequest(user.OrganizationId.Value);
            return BuildImpersonationStatusResponse(activeImpersonation);
        }

        public JsonResult GetMyImpersonationStatus()
        {
            if (!User.Identity.IsAuthenticated || !ImpersonationSessionHelper.IsSessionImpersonating(Session))
            {
                return Json(new { secondsLeft = 0 }, JsonRequestBehavior.AllowGet);
            }

            var status = ImpersonationSessionHelper.ResolveMyImpersonationStatus(Session, _unitOfWork, Url);
            return Json(new
            {
                secondsLeft = status.SecondsLeft,
                redirectUrl = status.RedirectUrl
            }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult CheckRequestStatus(int requestId)
        {
            var request = _unitOfWork.Repository<ImpersonationRequest>().GetById(requestId);
            if (request == null)
            {
                return Json(new { status = "NotFound" }, JsonRequestBehavior.AllowGet);
            }

            if (!CanViewImpersonationRequestStatus(request))
            {
                return Json(new { status = "Forbidden" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { status = request.Status.ToString() }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult HandleImpersonationRequest(int requestId, bool approved, int? durationMinutes = null, string notes = null)
        {
            var request = _unitOfWork.Repository<ImpersonationRequest>().GetById(requestId);
            if (!CanHandleImpersonationRequest(request))
            {
                return HttpNotFound();
            }

            if (request.Status == ImpersonationRequestStatus.Pending)
            {
                request.Status = approved ? ImpersonationRequestStatus.Approved : ImpersonationRequestStatus.Rejected;
                request.DecisionDate = DateTime.Now;
                request.AdminNotes = notes;
                if (approved)
                {
                    var resolvedMinutes = ImpersonationDurationOptions.ResolveMinutes(durationMinutes);
                    request.ExpiryDate = DateTime.Now.AddMinutes(resolvedMinutes);
                }

                _unitOfWork.Repository<ImpersonationRequest>().Update(request);
                _unitOfWork.SaveChanges();

                _auditWriter.Write(
                    approved ? "IMPERSONATION_APPROVED" : "IMPERSONATION_REJECTED",
                    "ImpersonationRequest",
                    requestId.ToString(),
                    null,
                    null);

                TempData["Message"] = approved
                    ? "Elevation request approved for " + ImpersonationDurationOptions.FormatMinutes(ImpersonationDurationOptions.ResolveMinutes(durationMinutes)) + "."
                    : "Elevation request rejected.";

                if (!approved)
                {
                    Session["RejectionNotification"] = new
                    {
                        OrganizationId = request.OrganizationId,
                        RequestedBy = request.RequestedBy,
                        AdminNotes = notes
                    };
                }
            }

            return RedirectToAction("Index");
        }

        private ApplicationUser GetImpersonationScopeUser()
        {
            if (!User.Identity.IsAuthenticated || !IsCurrentUserCompanyAdmin())
            {
                return null;
            }

            var userId = User.GetUserId();
            return string.IsNullOrWhiteSpace(userId) ? null : _unitOfWork.Writer<ApplicationUser>().GetById(userId);
        }

        private ImpersonationRequest GetActiveImpersonationRequest(int organizationId)
        {
            return _unitOfWork.Repository<ImpersonationRequest>().Query()
                .Where(r => r.OrganizationId == organizationId && r.Status == ImpersonationRequestStatus.Active)
                .OrderByDescending(r => r.ExpiryDate)
                .FirstOrDefault();
        }

        private JsonResult BuildImpersonationStatusResponse(ImpersonationRequest activeImpersonation)
        {
            var organizationSlug = GetCurrentOrganizationSlug();
            var unlockUrl = ImpersonationSessionHelper.BuildTenantAdminPostUnlockUrl(Url, organizationSlug);
            if (activeImpersonation == null)
            {
                return Json(new { isLocked = false, unlockUrl = unlockUrl }, JsonRequestBehavior.AllowGet);
            }

            var secondsLeft = activeImpersonation.ExpiryDate.HasValue
                ? (int)(activeImpersonation.ExpiryDate.Value - DateTime.Now).TotalSeconds
                : 3600;

            return Json(new
            {
                isLocked = true,
                expiry = activeImpersonation.ExpiryDate.HasValue ? activeImpersonation.ExpiryDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                secondsLeft = Math.Max(0, secondsLeft),
                unlockUrl = unlockUrl
            }, JsonRequestBehavior.AllowGet);
        }

        private bool CanHandleImpersonationRequest(ImpersonationRequest request)
        {
            return request != null
                && string.Equals(request.RequestedFrom, User.Identity.Name, StringComparison.OrdinalIgnoreCase)
                && IsCurrentUserCompanyAdmin();
        }

        private bool CanViewImpersonationRequestStatus(ImpersonationRequest request)
        {
            if (request == null)
            {
                return false;
            }

            if (_organizationScope != null && _organizationScope.IsActualPlatformAdmin())
            {
                return string.Equals(request.RequestedBy, User.Identity.Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(request.RequestedFrom, User.Identity.Name, StringComparison.OrdinalIgnoreCase);
            }

            var user = GetImpersonationScopeUser();
            if (user == null || !user.OrganizationId.HasValue)
            {
                return false;
            }

            return request.OrganizationId == user.OrganizationId.Value
                && (IsCurrentUserCompanyAdmin()
                    || string.Equals(request.RequestedFrom, User.Identity.Name, StringComparison.OrdinalIgnoreCase));
        }

        private string GetCurrentOrganizationSlug()
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            if (!string.IsNullOrWhiteSpace(tenantSlug))
            {
                return tenantSlug;
            }

            var user = GetImpersonationScopeUser();
            if (user != null && user.OrganizationId.HasValue)
            {
                return TenantUrlHelper.ResolveOrganizationSlug(_unitOfWork, user.OrganizationId.Value);
            }

            return null;
        }
    }
}
