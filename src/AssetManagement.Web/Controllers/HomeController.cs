using System;
using System.Web.Mvc;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Forbidden()
        {
            Response.StatusCode = 403;
            // Keep the app page: without this IIS httpErrors may replace the 403 body with its own error page.
            Response.TrySkipIisCustomErrors = true;
            ViewBag.HideNavbar = true;
            return View();
        }

        /// <summary>
        /// Old /{tenant}/Home/Help address of the how-to page: sends the user to the hidden /{tenant}/HelpVideos page.
        /// Without a tenant in the URL there is no page to show. [TenantAuthorize]: anonymous users go to the login page
        /// and are never told where the hidden page is (HomeController itself is public).
        /// </summary>
        [AssetManagement.Web.Filters.TenantAuthorize]
        public ActionResult Help()
        {
            var tenant = TenantUrlHelper.GetTenantToken(RouteData);
            if (!TenantUrlHelper.IsValidTenantSlug(tenant))
            {
                return HttpNotFound();
            }

            return TenantUrlHelper.CreateTenantRedirect(tenant, "HelpVideos", "Index");
        }

        public ActionResult Privacy()
        {
            return BuildLegalDocumentView(true);
        }

        public ActionResult Terms()
        {
            return BuildLegalDocumentView(false);
        }

        private ActionResult BuildLegalDocumentView(bool isPrivacyPage)
        {
            var kind = ResolveLegalDocumentRelationship();
            ViewBag.HideNavbar = true;
            ViewBag.LegalDocTenant = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.LegalDocPage = isPrivacyPage ? "privacy" : "terms";
            ViewBag.CompanyName = LegalPolicyDefaults.CompanyName;
            ViewBag.ContactEmail = LegalPolicyDefaults.ContactEmail;
            ViewBag.ContactAddress = LegalPolicyDefaults.ContactAddress;
            ViewBag.LastUpdated = LegalPolicyDefaults.LastUpdated;
            ViewBag.LegalRelationship = kind;

            if (isPrivacyPage)
            {
                ViewBag.PrivacyVersionLabel = LegalPolicyDefaults.GetPrivacyVersion(kind);
                return View("Privacy");
            }

            ViewBag.TermsVersionLabel = LegalPolicyDefaults.GetTermsVersion(kind);
            return View("Terms");
        }

        private LegalRelationshipKind ResolveLegalDocumentRelationship()
        {
            if (User != null && User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userId = User.GetUserId();
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    return ResolveRelationshipForUser(userId);
                }
            }

            var pendingUserId = LegalConsentSession.TryReadUserId(Session);
            if (!string.IsNullOrWhiteSpace(pendingUserId) && LegalConsentSession.IsFresh(Session))
            {
                return ResolveRelationshipForUser(pendingUserId);
            }

            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            return string.IsNullOrWhiteSpace(tenantSlug)
                ? LegalRelationshipKind.PlatformAdmin
                : LegalRelationshipKind.TenantUser;
        }

        private static LegalRelationshipKind ResolveRelationshipForUser(string userId)
        {
            var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return LegalRelationshipKind.TenantUser;
            }

            var users = new UserAccountRepository(connectionFactory);
            var user = users.FindById(userId);
            if (user == null)
            {
                return LegalRelationshipKind.TenantUser;
            }

            return LegalPolicyDefaults.ResolveFromRoleAndOrganization(
                users.FindRoleNameByUserId(userId),
                user.OrganizationId);
        }
    }
}
