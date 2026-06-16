using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Web.Helpers
{
    public static class ImpersonationSessionHelper
    {
        public static void ApplySession(HttpSessionStateBase session, ImpersonationRequest request, Organization organization)
        {
            session["ImpersonatedRequestId"] = request.Id;
            session["ImpersonatedOrganizationId"] = request.OrganizationId;
            session["ImpersonationReason"] = request.Reason ?? "Not specified";
            session["ImpersonatedOrganizationName"] = organization != null ? organization.Name : "Tenant";
            session["ImpersonationExpiry"] = request.ExpiryDate;
        }

        public static bool IsSessionImpersonating(HttpSessionStateBase session)
        {
            return session != null && session["ImpersonatedOrganizationId"] != null;
        }

        public static ImpersonationRequest GetSessionRequest(HttpSessionStateBase session, IUnitOfWork unitOfWork)
        {
            if (session == null || unitOfWork == null)
            {
                return null;
            }

            var requestId = session["ImpersonatedRequestId"] as int?;
            if (!requestId.HasValue)
            {
                return null;
            }

            return unitOfWork.Repository<ImpersonationRequest>().GetById(requestId.Value);
        }

        public static bool IsRequestActive(ImpersonationRequest request)
        {
            return request != null && !IsRequestExpired(request);
        }

        public static bool IsRequestExpired(ImpersonationRequest request)
        {
            if (request == null)
            {
                return true;
            }

            if (request.Status != ImpersonationRequestStatus.Active &&
                request.Status != ImpersonationRequestStatus.Approved)
            {
                return true;
            }

            if (request.ExpiryDate.HasValue && request.ExpiryDate.Value < DateTime.Now)
            {
                return true;
            }

            return false;
        }

        public static void ClearSession(HttpSessionStateBase session)
        {
            if (session == null)
            {
                return;
            }

            session.Remove("ImpersonatedRequestId");
            session.Remove("ImpersonatedOrganizationId");
            session.Remove("ImpersonationReason");
            session.Remove("ImpersonatedOrganizationName");
            session.Remove("ImpersonationExpiry");
        }

        public static void ExpireRequest(ImpersonationRequest request, IUnitOfWork unitOfWork)
        {
            if (request == null || unitOfWork == null)
            {
                return;
            }

            if (request.Status == ImpersonationRequestStatus.Active ||
                request.Status == ImpersonationRequestStatus.Approved)
            {
                request.Status = ImpersonationRequestStatus.Expired;
                unitOfWork.Repository<ImpersonationRequest>().Update(request);
            }
        }

        public static void ExpireStaleImpersonationRequestsForOrganization(int organizationId, IUnitOfWork unitOfWork)
        {
            if (unitOfWork == null)
            {
                return;
            }

            var now = DateTime.Now;
            var staleRequests = unitOfWork.Repository<ImpersonationRequest>().Query()
                .Where(r => r.OrganizationId == organizationId &&
                    r.ExpiryDate.HasValue &&
                    r.ExpiryDate < now)
                .ToList()
                .Where(r => r.Status == ImpersonationRequestStatus.Active ||
                    r.Status == ImpersonationRequestStatus.Approved)
                .ToList();

            if (!staleRequests.Any())
            {
                return;
            }

            foreach (var staleRequest in staleRequests)
            {
                staleRequest.Status = ImpersonationRequestStatus.Expired;
                unitOfWork.Repository<ImpersonationRequest>().Update(staleRequest);
            }

            unitOfWork.SaveChanges();
        }

        public static string BuildPlatformAdminPostExpiryUrl(UrlHelper url, int? organizationId)
        {
            if (url == null)
            {
                return organizationId.HasValue
                    ? string.Format("/Platform/Organizations/OrganizationDetails/{0}", organizationId.Value)
                    : "/Platform/Organizations/Index";
            }

            return organizationId.HasValue
                ? url.Action("OrganizationDetails", "Organizations", new { area = "Platform", id = organizationId.Value })
                : url.Action("Index", "Organizations", new { area = "Platform" });
        }

        public static string BuildTenantAdminPostUnlockUrl(UrlHelper url, string organizationSlug)
        {
            if (url == null)
            {
                return string.IsNullOrWhiteSpace(organizationSlug)
                    ? "/Dashboard/Index"
                    : string.Format("/{0}/Dashboard/Index", organizationSlug);
            }

            return TenantUrlHelper.TenantRouteUrl(url, organizationSlug, "Index", "Dashboard")
                ?? url.Action("Index", "Dashboard");
        }

        public static string BuildTenantAdminPostUnlockUrl(UrlHelper url)
        {
            return BuildTenantAdminPostUnlockUrl(url, TenantUrlHelper.GetTenantToken(url != null ? url.RequestContext.HttpContext : null));
        }

        public static bool TryEndActiveImpersonation(
            HttpSessionStateBase session,
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            string actorUsername)
        {
            if (session == null || unitOfWork == null || !IsSessionImpersonating(session))
            {
                return false;
            }

            var organizationId = session["ImpersonatedOrganizationId"] as int?;
            var sessionRequestId = session["ImpersonatedRequestId"] as int?;

            if (sessionRequestId.HasValue && sessionRequestId.Value > 0)
            {
                var sessionRequest = unitOfWork.Repository<ImpersonationRequest>().GetById(sessionRequestId.Value);
                if (sessionRequest != null &&
                    (sessionRequest.Status == ImpersonationRequestStatus.Active ||
                     sessionRequest.Status == ImpersonationRequestStatus.Approved))
                {
                    sessionRequest.Status = ImpersonationRequestStatus.Expired;
                    unitOfWork.Repository<ImpersonationRequest>().Update(sessionRequest);
                }
            }

            if (organizationId.HasValue && !string.IsNullOrWhiteSpace(actorUsername))
            {
                var related = unitOfWork.Repository<ImpersonationRequest>().Query()
                    .Where(r => r.OrganizationId == organizationId.Value &&
                        r.RequestedBy == actorUsername &&
                        (r.Status == ImpersonationRequestStatus.Active || r.Status == ImpersonationRequestStatus.Approved))
                    .ToList();

                foreach (var request in related)
                {
                    request.Status = ImpersonationRequestStatus.Expired;
                    unitOfWork.Repository<ImpersonationRequest>().Update(request);
                }
            }

            unitOfWork.SaveChanges();
            ClearSession(session);

            if (auditWriter != null)
            {
                auditWriter.Write(
                    "IMPERSONATION_STOP",
                    "Organization",
                    organizationId.HasValue ? organizationId.Value.ToString() : null,
                    null,
                    null);
            }

            return true;
        }

        public static ImpersonationStatusResult ResolveMyImpersonationStatus(
            HttpSessionStateBase session,
            IUnitOfWork unitOfWork,
            UrlHelper url)
        {
            if (session == null || !IsSessionImpersonating(session))
            {
                return new ImpersonationStatusResult { SecondsLeft = 0 };
            }

            var organizationId = session["ImpersonatedOrganizationId"] as int?;
            var request = GetSessionRequest(session, unitOfWork);
            if (!IsRequestActive(request))
            {
                if (request != null)
                {
                    ExpireRequest(request, unitOfWork);
                    unitOfWork.SaveChanges();
                }

                ClearSession(session);
                return new ImpersonationStatusResult
                {
                    SecondsLeft = 0,
                    RedirectUrl = BuildPlatformAdminPostExpiryUrl(url, organizationId)
                };
            }

            var secondsLeft = request.ExpiryDate.HasValue
                ? (int)(request.ExpiryDate.Value - DateTime.Now).TotalSeconds
                : 0;
            secondsLeft = Math.Max(0, secondsLeft);

            if (secondsLeft == 0)
            {
                ExpireRequest(request, unitOfWork);
                unitOfWork.SaveChanges();
                ClearSession(session);
                return new ImpersonationStatusResult
                {
                    SecondsLeft = 0,
                    RedirectUrl = BuildPlatformAdminPostExpiryUrl(url, organizationId)
                };
            }

            return new ImpersonationStatusResult
            {
                SecondsLeft = secondsLeft,
                RedirectUrl = BuildPlatformAdminPostExpiryUrl(url, organizationId)
            };
        }

        public static bool TryClearStaleImpersonationSession(
            HttpSessionStateBase session,
            IUnitOfWork unitOfWork,
            IAuditWriter auditWriter,
            string actorUsername)
        {
            if (session == null || unitOfWork == null || !IsSessionImpersonating(session))
            {
                return false;
            }

            var organizationId = session["ImpersonatedOrganizationId"] as int?;
            var request = GetSessionRequest(session, unitOfWork);
            if (IsRequestActive(request))
            {
                return false;
            }

            if (request != null)
            {
                ExpireRequest(request, unitOfWork);
            }

            if (organizationId.HasValue && !string.IsNullOrWhiteSpace(actorUsername))
            {
                var related = unitOfWork.Repository<ImpersonationRequest>().Query()
                    .Where(r => r.OrganizationId == organizationId.Value &&
                        r.RequestedBy == actorUsername &&
                        (r.Status == ImpersonationRequestStatus.Active || r.Status == ImpersonationRequestStatus.Approved))
                    .ToList();

                foreach (var relatedRequest in related)
                {
                    relatedRequest.Status = ImpersonationRequestStatus.Expired;
                    unitOfWork.Repository<ImpersonationRequest>().Update(relatedRequest);
                }
            }

            unitOfWork.SaveChanges();
            ClearSession(session);

            if (auditWriter != null)
            {
                auditWriter.Write(
                    "IMPERSONATION_EXPIRED",
                    "Organization",
                    organizationId.HasValue ? organizationId.Value.ToString() : null,
                    null,
                    null);
            }

            return true;
        }

        public static bool TryRestoreAfterLogout(string username, HttpSessionStateBase session, IUnitOfWork unitOfWork, IAuditWriter auditWriter)
        {
            if (string.IsNullOrWhiteSpace(username) || session == null)
            {
                return false;
            }

            var request = unitOfWork.Repository<ImpersonationRequest>().Query()
                .Where(r => r.RequestedBy == username && r.OrganizationId.HasValue)
                .OrderByDescending(r => r.DecisionDate ?? r.RequestDate)
                .ToList()
                .FirstOrDefault(r => r.Status == ImpersonationRequestStatus.Active);

            if (request == null)
            {
                return false;
            }

            if (IsRequestExpired(request))
            {
                ExpireRequest(request, unitOfWork);
                unitOfWork.SaveChanges();
                return false;
            }

            var organization = unitOfWork.Repository<Organization>().GetById(request.OrganizationId.Value);
            if (organization == null)
            {
                return false;
            }

            ApplySession(session, request, organization);

            auditWriter.Write(
                "IMPERSONATION_RESUME",
                "Organization",
                request.OrganizationId.ToString(),
                null,
                "{\"Reason\":\"" + (request.Reason ?? string.Empty) + "\"}");

            return true;
        }
    }

    public sealed class ImpersonationStatusResult
    {
        public int SecondsLeft { get; set; }

        public string RedirectUrl { get; set; }
    }
}
