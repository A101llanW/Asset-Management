using System;
using System.Globalization;
using System.Linq;
using System.Web.Mvc;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;
using AssetManagement.Domain.Enums;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Security;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Models;
using AssetManagement.Web.Security;
using AssetManagement.Web.ViewModels;
using Newtonsoft.Json;
using System.Text;

namespace AssetManagement.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserAccountService _userAccountService;
        private readonly IAuthorizationService _authorizationService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOrganizationLicenseService _licenseService;
        private readonly IAccountSecurityService _accountSecurityService;
        private readonly IAuthFlowRateLimiter _authFlowRateLimiter;
        private readonly IUserInvitationService _userInvitationService;

        public AccountController()
        {
            _userAccountService = DependencyResolver.Current.GetService<IUserAccountService>();
            _authorizationService = DependencyResolver.Current.GetService<IAuthorizationService>();
            _unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            _auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _licenseService = DependencyResolver.Current.GetService<IOrganizationLicenseService>();
            _accountSecurityService = DependencyResolver.Current.GetService<IAccountSecurityService>();
            _authFlowRateLimiter = DependencyResolver.Current.GetService<IAuthFlowRateLimiter>();
            _userInvitationService = DependencyResolver.Current.GetService<IUserInvitationService>();
        }

        public ActionResult Login(string returnUrl)
        {
            var tenantToken = TenantUrlHelper.GetTenantToken(RouteData);
            if (!string.IsNullOrWhiteSpace(tenantToken) && TenantUrlHelper.IsPlausibleTenantToken(tenantToken))
            {
                var organization = TenantResolutionHelper.ResolveOrganization(_unitOfWork, tenantToken);
                if (organization == null)
                {
                    ViewBag.TenantToken = tenantToken;
                    return View("OrganizationNotFound");
                }

                if (organization.Slug != null
                    && !organization.Slug.Equals(tenantToken, System.StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(TenantUrlHelper.BuildTenantLoginPath(organization.Slug, returnUrl));
                }
            }

            ViewBag.ReturnUrl = ParseReturnPathOrNull(returnUrl);
            ConfigureLoginViewBag();
            return View();
        }

        [Authorize]
        public ActionResult Landing()
        {
            var userId = User.GetUserId();
            if (IsPlatformAdminUser(userId))
            {
                return PlatformAdminHelper.CreateOrganizationsRedirect();
            }

            return RedirectToLocal(null, userId, TenantUrlHelper.GetTenantToken(RouteData));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl, string captcha)
        {
            ConfigureLoginViewBag();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (_userAccountService == null || _accountSecurityService == null)
            {
                ModelState.AddModelError("", "Login is unavailable because application services failed to start.");
                return View(model);
            }

            var captchaError = ValidateLoginCaptcha(captcha);
            if (captchaError != null)
            {
                ModelState.AddModelError("", captchaError);
                return View(model);
            }

            var tenantSlug = ResolveLoginTenantSlug();
            var organizationId = ResolveOrganizationId(tenantSlug);
            var clientIp = GetClientIpAddress();

            if (_accountSecurityService.IsLoginIpRateLimited(clientIp))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.LoginIpRateLimited());
                return View(model);
            }

            if (_accountSecurityService.IsAccountLocked(model.Email, organizationId))
            {
                var lockoutEnd = _accountSecurityService.GetLockoutEndTimeUtc(model.Email, organizationId);
                var minutesRemaining = lockoutEnd.HasValue
                    ? (int)System.Math.Ceiling((lockoutEnd.Value - System.DateTime.UtcNow).TotalMinutes)
                    : 30;
                if (minutesRemaining < 1)
                {
                    minutesRemaining = 1;
                }

                _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, false, organizationId, "Account locked");
                ModelState.AddModelError("", AuthenticationErrorMessages.LoginAccountLocked(minutesRemaining));
                return View(model);
            }

            var resolvedEmail = DemoLoginEmailHelper.ResolveLoginEmail(model.Email, tenantSlug);
            var candidates = DiscoverLoginCandidates(resolvedEmail, organizationId);
            var disambiguationResult = HandleLoginDisambiguation(model, candidates, organizationId, tenantSlug);
            if (disambiguationResult != null)
            {
                return disambiguationResult;
            }

            var primaryUser = candidates.Count == 1 ? candidates[0] : null;
            string userId;
            if (primaryUser != null)
            {
                var verifyResult = PasswordHasher.VerifyHashedPassword(primaryUser.PasswordHash, model.Password);
                if (verifyResult == PasswordVerificationResult.Failed)
                {
                    _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, false, organizationId, "Invalid credentials");
                    var remaining = _accountSecurityService.GetRemainingLoginAttempts(model.Email, organizationId);
                    ModelState.AddModelError("", AuthenticationErrorMessages.LoginFailure(model.Email, tenantSlug, remaining));
                    return View(model);
                }

                userId = primaryUser.Id;
                if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded && _userAccountService != null)
                {
                    _userAccountService.RehashPasswordOnLogin(userId, model.Password);
                }
            }
            else if (!_userAccountService.ValidateCredentials(model.Email, model.Password, tenantSlug, out userId))
            {
                _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, false, organizationId, "Invalid credentials");
                var remaining = _accountSecurityService.GetRemainingLoginAttempts(model.Email, organizationId);
                ModelState.AddModelError("", AuthenticationErrorMessages.LoginFailure(model.Email, tenantSlug, remaining));
                return View(model);
            }

            _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, true, organizationId, null);
            _accountSecurityService.ClearFailedLoginAttempts(model.Email, organizationId);

            return CompleteLoginAfterCredentials(model, ParseReturnPathOrNull(returnUrl), userId, tenantSlug);
        }

        [Authorize]
        [HttpGet]
        public ActionResult Profile()
        {
            var userId = User.GetUserId();
            var user = _userAccountService == null ? null : FindUserById(userId);
            if (user == null)
            {
                return HttpNotFound();
            }

            var model = new ProfileViewModel
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                Phone = user.Phone,
                RoleName = new UserAccountRepository(
                    DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>())
                    .FindRoleNameByUserId(userId)
            };

            if (user.OrganizationId.HasValue)
            {
                var org = _unitOfWork.Repository<Organization>().GetById(user.OrganizationId.Value);
                model.OrganizationName = org == null ? null : org.Name;
            }
            else
            {
                model.OrganizationName = "Platform";
            }

            return View(model);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Profile(ProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                PopulateProfileDisplayFields(model);
                return View(model);
            }

            var userId = User.GetUserId();
            if (_userAccountService == null || !_userAccountService.UpdateProfile(userId, model.FirstName, model.LastName, model.Phone))
            {
                ModelState.AddModelError("", "Could not update your profile.");
                PopulateProfileDisplayFields(model);
                return View(model);
            }

            TempData["Message"] = "Your profile has been updated successfully.";
            return RedirectToAction("Profile");
        }

        [Authorize]
        [HttpGet]
        public ActionResult ChangePassword()
        {
            ViewBag.PasswordPolicyMessage = PasswordPolicy.GetPolicyMessage();
            return View(new ChangePasswordViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(ChangePasswordViewModel model)
        {
            ViewBag.PasswordPolicyMessage = PasswordPolicy.GetPolicyMessage();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!string.Equals(model.NewPassword, model.ConfirmPassword))
            {
                ModelState.AddModelError("ConfirmPassword", "The new password and confirmation password do not match.");
                return View(model);
            }

            var userId = User.GetUserId();
            if (_userAccountService == null)
            {
                ModelState.AddModelError("", "Password change is unavailable.");
                return View(model);
            }

            var policyErrors = _userAccountService.GetPasswordPolicyErrors(model.NewPassword).ToList();
            if (policyErrors.Count > 0)
            {
                foreach (var error in policyErrors)
                {
                    ModelState.AddModelError("", error);
                }

                return View(model);
            }

            if (!_userAccountService.ChangePassword(userId, model.CurrentPassword, model.NewPassword))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.ChangePasswordFailure());
                return View(model);
            }

            if (_accountSecurityService != null)
            {
                _accountSecurityService.RotateUserAccessToken(userId);
            }

            var updatedUser = FindUserById(userId);
            if (updatedUser != null)
            {
                updatedUser.RequirePasswordChange = false;
                var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
                if (connectionFactory != null)
                {
                    new UserAccountRepository(connectionFactory).Update(updatedUser);
                }

                CurrentUserExtensions.SetAuthCookie(Response, updatedUser, false, Request.UserAgent, false);
            }

            TempData["Message"] = "Your password has been updated successfully.";
            return RedirectToAction("Profile");
        }

        [HttpGet]
        public ActionResult VerifyEmail()
        {
            var userId = Session["PendingEmailVerificationUserId"] as string ?? User.GetUserId();
            var user = FindUserById(userId);
            if (user == null)
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.EmailHint = _accountSecurityService != null ? _accountSecurityService.MaskEmail(user.Email) : user.Email;
            return View("VerifyEmail");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyEmailSubmit(string code)
        {
            return VerifyEmail(code);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyEmail(string code)
        {
            var userId = Session["PendingEmailVerificationUserId"] as string ?? User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            if (_accountSecurityService == null || !_accountSecurityService.ValidateEmailVerificationCode(userId, code))
            {
                ModelState.AddModelError("", "Invalid or expired verification code.");
                return VerifyEmail();
            }

            _accountSecurityService.MarkEmailVerified(userId);
            Session.Remove("PendingEmailVerificationUserId");
            var rememberMe = Session["PendingEmailVerificationRememberMe"] as bool? ?? false;
            var returnUrl = Session["PendingEmailVerificationReturnUrl"] as string;
            Session.Remove("PendingEmailVerificationRememberMe");
            Session.Remove("PendingEmailVerificationReturnUrl");
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            var user = FindUserById(userId);
            if (user != null && user.RequirePasswordChange)
            {
                CurrentUserExtensions.SetAuthCookie(Response, user, rememberMe, Request.UserAgent, true);
                return RedirectToAction("ChangePassword", new { tenant = tenantSlug });
            }

            return IssueAuthCookieAndRedirect(userId, user != null ? user.Email : null, rememberMe, returnUrl, tenantSlug);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ResendEmailVerificationCode()
        {
            var userId = Session["PendingEmailVerificationUserId"] as string ?? User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId) || _accountSecurityService == null)
            {
                return Json(new { success = false, message = "Session expired." });
            }

            return Json(new
            {
                success = _accountSecurityService.SendEmailVerificationCode(userId),
                message = "If email delivery is configured, a new verification code has been sent."
            });
        }

        private System.Collections.Generic.IList<ApplicationUser> DiscoverLoginCandidates(string email, int? organizationId)
        {
            var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return new System.Collections.Generic.List<ApplicationUser>();
            }

            return new UserAccountRepository(connectionFactory).FindActiveByEmail(email, organizationId);
        }

        private ActionResult HandleLoginDisambiguation(
            LoginViewModel model,
            System.Collections.Generic.IList<ApplicationUser> candidates,
            int? organizationId,
            string tenantSlug)
        {
            if (candidates == null || candidates.Count <= 1)
            {
                if (organizationId.HasValue && candidates != null && candidates.Count == 0)
                {
                    var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
                    if (connectionFactory != null)
                    {
                        var elsewhere = new UserAccountRepository(connectionFactory).FindActiveByEmail(model.Email, null)
                            .Where(u => u.OrganizationId.HasValue && u.OrganizationId.Value != organizationId.Value)
                            .ToList();
                        if (elsewhere.Count > 0)
                        {
                            ViewBag.MultiCandidates = BuildPortalCandidates(elsewhere, connectionFactory);
                            ModelState.AddModelError("", "No account for this email in this organization portal. Select your organization below.");
                            return View(model);
                        }
                    }
                }

                return null;
            }

            if (!organizationId.HasValue)
            {
                var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
                ViewBag.MultiCandidates = BuildPortalCandidates(candidates, connectionFactory);
                ModelState.AddModelError("", "We found multiple accounts for this email. Select the correct portal below.");
                return View(model);
            }

            return null;
        }

        private static System.Collections.Generic.IList<LoginPortalCandidate> BuildPortalCandidates(
            System.Collections.Generic.IEnumerable<ApplicationUser> users,
            Infrastructure.Persistence.ISqlConnectionFactory connectionFactory)
        {
            var repository = new UserAccountRepository(connectionFactory);
            return users.Select(u => new LoginPortalCandidate
            {
                UserId = u.Id,
                Email = u.Email,
                OrganizationId = u.OrganizationId,
                OrganizationName = u.OrganizationId.HasValue ? repository.FindOrganizationNameById(u.OrganizationId.Value) : "Platform",
                OrganizationSlug = u.OrganizationId.HasValue ? repository.FindOrganizationSlugById(u.OrganizationId.Value) : null
            }).ToList();
        }

        private void EnsureLoginAccessToken(ApplicationUser user)
        {
            if (user == null || !string.IsNullOrWhiteSpace(user.AccessToken))
            {
                return;
            }

            user.AccessToken = SecurePasswordGenerator.GenerateAccessToken();
            var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
            if (connectionFactory != null)
            {
                new UserAccountRepository(connectionFactory).Update(user);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmLegalConsent(bool acceptLegalTerms, string returnUrl)
        {
            ConfigureLoginViewBag();
            var safeReturnUrl = ParseReturnPathOrNull(returnUrl);
            var userId = LegalConsentSession.TryReadUserId(Session);
            if (string.IsNullOrWhiteSpace(userId) || !LegalConsentSession.IsFresh(Session))
            {
                LegalConsentSession.Clear(Session);
                TempData["Error"] = "Your sign-in confirmation expired. Please sign in again.";
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            if (!acceptLegalTerms)
            {
                ModelState.AddModelError("", "Please accept the Terms and Conditions and Privacy Policy to continue.");
                ViewBag.ShowLegalConsentModal = true;
                ConfigureLegalConsentViewBag(safeReturnUrl);
                return View("Login", new LoginViewModel { Email = Session[LegalConsentSession.PendingEmailSession] as string });
            }

            _accountSecurityService.RecordLegalAcceptance(userId);

            var rememberMe = Session[LegalConsentSession.PendingRememberMeSession] as bool? ?? false;
            var email = Session[LegalConsentSession.PendingEmailSession] as string;
            var pendingReturnUrl = ParseReturnPathOrNull(Session[LegalConsentSession.PendingReturnUrlSession] as string);
            LegalConsentSession.Clear(Session);

            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            return CompleteLoginAfterLegalConsent(email, rememberMe, pendingReturnUrl ?? safeReturnUrl, userId, tenantSlug);
        }

        [HttpGet]
        public ActionResult SetupMfa()
        {
            var userId = Session["ForcedMfaSetupUserId"] as string;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            var user = FindUserById(userId);
            if (user == null)
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.Email = user.Email;
            ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
            return View("SetupMfa");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetupMfa(string method, string code)
        {
            var userId = Session["ForcedMfaSetupUserId"] as string;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            int minutesRemaining;
            if (!_authFlowRateLimiter.IsMfaVerifyAllowed(userId, out minutesRemaining))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.MfaVerifyLockout(minutesRemaining));
                ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
                ViewBag.Email = FindUserById(userId) != null ? FindUserById(userId).Email : null;
                ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
                return View("SetupMfa");
            }

            if (!_accountSecurityService.ValidateMfaCode(userId, code))
            {
                _authFlowRateLimiter.RecordMfaVerifyFailure(userId);
                ModelState.AddModelError("", AuthenticationErrorMessages.MfaSetupInvalidCode());
                ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
                ViewBag.Email = FindUserById(userId) != null ? FindUserById(userId).Email : null;
                ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
                return View("SetupMfa");
            }

            _authFlowRateLimiter.ClearMfaVerifyFailures(userId);
            _accountSecurityService.EnableMfa(userId, method);
            Session.Remove("ForcedMfaSetupUserId");
            return IssueAuthCookieAndRedirect(
                userId,
                Session["PendingMfaEmail"] as string,
                Session["PendingMfaRememberMe"] as bool? ?? false,
                ParseReturnPathOrNull(Session["PendingMfaReturnUrl"] as string),
                TenantUrlHelper.GetTenantToken(RouteData));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SendSetupMfaCode(string method)
        {
            var userId = Session["ForcedMfaSetupUserId"] as string;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { success = false, message = "Session expired." });
            }

            if (!_authFlowRateLimiter.TryAcquireMfaSend(userId))
            {
                Response.StatusCode = 429;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = AuthenticationErrorMessages.MfaSendRateLimited() });
            }

            if (!_accountSecurityService.SendMfaCode(userId))
            {
                return Json(new { success = false, message = AuthenticationErrorMessages.MfaSendServiceFailure() });
            }

            var devMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
            return Json(new { success = true, message = AuthenticationErrorMessages.MfaSendSuccess(devMode) });
        }

        [HttpGet]
        public ActionResult VerifyMfa()
        {
            var userId = Session["PendingMfaUserId"] as string;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            var user = FindUserById(userId);
            if (user == null)
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.EmailHint = _accountSecurityService.MaskEmail(user.Email);
            if (!_authFlowRateLimiter.TryAcquireMfaSend(userId))
            {
                ViewBag.MfaSendError = AuthenticationErrorMessages.MfaSendRateLimited();
            }
            else if (!_accountSecurityService.SendMfaCode(userId))
            {
                ViewBag.MfaSendError = AuthenticationErrorMessages.MfaSendFailure();
            }

            ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
            return View("VerifyMfa");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VerifyMfa(string code)
        {
            var userId = Session["PendingMfaUserId"] as string;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            int minutesRemaining;
            if (!_authFlowRateLimiter.IsMfaVerifyAllowed(userId, out minutesRemaining))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.MfaVerifyLockout(minutesRemaining));
                ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
                ViewBag.EmailHint = _accountSecurityService.MaskEmail(FindUserById(userId) != null ? FindUserById(userId).Email : null);
                ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
                return View("VerifyMfa");
            }

            if (!_accountSecurityService.ValidateMfaCode(userId, code))
            {
                _authFlowRateLimiter.RecordMfaVerifyFailure(userId);
                ModelState.AddModelError("", AuthenticationErrorMessages.MfaInvalidCode());
                ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
                ViewBag.EmailHint = _accountSecurityService.MaskEmail(FindUserById(userId) != null ? FindUserById(userId).Email : null);
                ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
                return View("VerifyMfa");
            }

            _authFlowRateLimiter.ClearMfaVerifyFailures(userId);
            _accountSecurityService.ClearMfaCode(userId);
            Session.Remove("PendingMfaUserId");
            return IssueAuthCookieAndRedirect(
                userId,
                Session["PendingMfaEmail"] as string,
                Session["PendingMfaRememberMe"] as bool? ?? false,
                ParseReturnPathOrNull(Session["PendingMfaReturnUrl"] as string),
                TenantUrlHelper.GetTenantToken(RouteData));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ResendMfaCode()
        {
            var userId = Session["PendingMfaUserId"] as string;
            if (string.IsNullOrWhiteSpace(userId))
            {
                return Json(new { success = false, message = "Session expired." });
            }

            if (!_authFlowRateLimiter.TryAcquireMfaSend(userId))
            {
                Response.StatusCode = 429;
                Response.TrySkipIisCustomErrors = true;
                return Json(new { success = false, message = AuthenticationErrorMessages.MfaSendRateLimited() });
            }

            if (!_accountSecurityService.SendMfaCode(userId))
            {
                return Json(new { success = false, message = AuthenticationErrorMessages.MfaResendServiceFailure() });
            }

            var devMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
            return Json(new { success = true, message = AuthenticationErrorMessages.MfaResendSuccess(devMode) });
        }

        [Authorize]
        [HttpGet]
        [ActionName("LogOff")]
        public ActionResult LogOffGet()
        {
            return PerformLogOff();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("LogOff")]
        public ActionResult LogOffPost()
        {
            return PerformLogOff();
        }

        private ActionResult PerformLogOff()
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            if (string.IsNullOrWhiteSpace(tenantSlug) && _organizationScope != null)
            {
                var userId = User.GetUserId();
                tenantSlug = TenantUrlHelper.ResolveOrganizationSlug(_unitOfWork, userId);
            }

            var actorName = User != null && User.Identity != null ? User.Identity.Name : null;
            ImpersonationSessionHelper.TryEndActiveImpersonation(Session, _unitOfWork, _auditWriter, actorName);

            AuthSessionHelper.SignOut(HttpContext);
            return RedirectToLogin(tenantSlug);
        }

        [Authorize]
        public ActionResult LicenseSuspended()
        {
            ConfigureLicenseStatusViewBag(LicenseStatus.Paused);
            return View();
        }

        [Authorize]
        public ActionResult LicenseExpired()
        {
            ConfigureLicenseStatusViewBag(LicenseStatus.Expired);
            return View();
        }

        public ActionResult ForgotPassword()
        {
            ConfigureForgotPasswordViewBag();
            return View();
        }

        public ActionResult Register()
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                TempData["Message"] = "Registration is only available through your organization portal.";
                return RedirectToAction("Login");
            }

            var organization = ResolveTenantOrganization(tenantSlug);
            if (organization == null)
            {
                return HttpNotFound();
            }

            if (!IsRegistrationAllowed(organization.Id))
            {
                TempData["Message"] = "New registrations are not available for this organization right now.";
                return RedirectToLogin(tenantSlug);
            }

            ConfigureRegisterViewBag(organization, tenantSlug);
            return View(new RegisterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(RegisterViewModel model)
        {
            var tenantSlug = ResolveLoginTenantSlug() ?? TenantUrlHelper.GetTenantToken(RouteData);
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                TempData["Message"] = "Registration is only available through your organization portal.";
                return RedirectToAction("Login");
            }

            var organization = ResolveTenantOrganization(tenantSlug);
            if (organization == null)
            {
                return HttpNotFound();
            }

            ConfigureRegisterViewBag(organization, tenantSlug);

            if (!IsRegistrationAllowed(organization.Id))
            {
                ModelState.AddModelError("", "New registrations are not available for this organization right now.");
                return View(model);
            }

            if (!_authFlowRateLimiter.TryAcquireRegistration(TenantUrlHelper.GetTenantToken(RouteData), AuthFlowClientAddressHelper.Resolve(HttpContext)))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.RegistrationRateLimited());
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!string.Equals(model.Password, model.ConfirmPassword))
            {
                ModelState.AddModelError("ConfirmPassword", "The password and confirmation password do not match.");
                return View(model);
            }

            var staffRoleId = ResolveStaffRoleId(organization.Id);
            if (!staffRoleId.HasValue)
            {
                ModelState.AddModelError(
                    "",
                    AuthenticationErrorMessages.IsGenericAuthMessagesEnabled()
                        ? AuthenticationErrorMessages.RegistrationFailure()
                        : "Registration is unavailable because the Staff role is not configured.");
                return View(model);
            }

            var createRequest = new UserAccountCreateRequest
            {
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Phone = model.Phone,
                RoleId = staffRoleId,
                OrganizationId = organization.Id
            };

            var result = _userAccountService.CreateUser(createRequest, model.Password);
            if (!result.Succeeded)
            {
                if (AuthenticationErrorMessages.IsGenericAuthMessagesEnabled())
                {
                    ModelState.AddModelError("", AuthenticationErrorMessages.RegistrationFailure());
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                return View(model);
            }

            TempData["Message"] = "Your account was created. Sign in with your email and password.";
            return RedirectToLogin(tenantSlug);
        }

        public ActionResult AcceptInvite(string code)
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                TempData["Message"] = "Invitations are only available through your organization portal.";
                return RedirectToAction("Login");
            }

            var organization = ResolveTenantOrganization(tenantSlug);
            if (organization == null)
            {
                return HttpNotFound();
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["Message"] = AuthenticationErrorMessages.InviteAcceptInvalidToken();
                return RedirectToLogin(tenantSlug);
            }

            var validation = _userInvitationService == null
                ? new UserInvitationValidationResult { IsValid = false }
                : _userInvitationService.ValidateInvitation(code, organization.Id);

            if (!validation.IsValid)
            {
                TempData["Message"] = AuthenticationErrorMessages.InviteAcceptInvalidToken();
                return RedirectToLogin(tenantSlug);
            }

            ConfigureAcceptInviteViewBag(organization, tenantSlug);
            return View(new AcceptInviteViewModel
            {
                Code = code,
                Email = validation.Email,
                EmailLocked = validation.EmailLocked
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptInvite(AcceptInviteViewModel model)
        {
            var tenantSlug = ResolveLoginTenantSlug() ?? TenantUrlHelper.GetTenantToken(RouteData);
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                TempData["Message"] = "Invitations are only available through your organization portal.";
                return RedirectToAction("Login");
            }

            var organization = ResolveTenantOrganization(tenantSlug);
            if (organization == null)
            {
                return HttpNotFound();
            }

            ConfigureAcceptInviteViewBag(organization, tenantSlug);

            if (!_authFlowRateLimiter.TryAcquireInviteAccept(tenantSlug, AuthFlowClientAddressHelper.Resolve(HttpContext)))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.InviteAcceptRateLimited());
                return View(model);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!string.Equals(model.Password, model.ConfirmPassword))
            {
                ModelState.AddModelError("ConfirmPassword", "The password and confirmation password do not match.");
                return View(model);
            }

            var acceptRequest = new UserInvitationAcceptRequest
            {
                Token = model.Code,
                OrganizationId = organization.Id,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Email = model.Email,
                Phone = model.Phone,
                Password = model.Password
            };

            var result = _userInvitationService == null
                ? new UserInvitationAcceptResult { Succeeded = false }
                : _userInvitationService.AcceptInvitation(acceptRequest);

            if (!result.Succeeded)
            {
                if (result.FailureReason == UserInvitationAcceptFailureReason.InvalidOrExpiredToken)
                {
                    ModelState.AddModelError("", AuthenticationErrorMessages.InviteAcceptInvalidToken());
                    return View(model);
                }

                if (result.FailureReason == UserInvitationAcceptFailureReason.EmailMismatch)
                {
                    foreach (var error in result.Errors ?? new string[0])
                    {
                        ModelState.AddModelError("", error);
                    }

                    return View(model);
                }

                if (result.FailureReason == UserInvitationAcceptFailureReason.PolicyViolation)
                {
                    foreach (var error in result.Errors ?? new string[0])
                    {
                        ModelState.AddModelError("Password", error);
                    }

                    return View(model);
                }

                if (AuthenticationErrorMessages.IsGenericAuthMessagesEnabled())
                {
                    ModelState.AddModelError("", AuthenticationErrorMessages.InviteAcceptFailure());
                }
                else
                {
                    foreach (var error in result.Errors ?? new string[0])
                    {
                        ModelState.AddModelError("", error);
                    }
                }

                return View(model);
            }

            TempData["Message"] = "Your account was created. Sign in with your email and password.";
            return RedirectToLogin(tenantSlug);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            ConfigureForgotPasswordViewBag();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var clientIp = Request.ServerVariables["HTTP_X_FORWARDED_FOR"] ?? Request.UserHostAddress;
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            int? organizationId = null;
            if (!string.IsNullOrWhiteSpace(tenantSlug))
            {
                var org = _unitOfWork.Repository<Organization>().Query()
                    .FirstOrDefault(o => o.Slug != null && o.Slug.Equals(tenantSlug, System.StringComparison.OrdinalIgnoreCase));
                if (org != null)
                {
                    organizationId = org.Id;
                }
            }

            if (_accountSecurityService != null && _accountSecurityService.IsForgotPasswordRateLimited(clientIp))
            {
                TempData["Message"] = AuthenticationErrorMessages.ForgotPasswordSuccess(
                    _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed());
                return RedirectToLogin(tenantSlug);
            }

            if (_accountSecurityService != null)
            {
                _accountSecurityService.RecordForgotPasswordAttempt(clientIp, model.Email, organizationId);
            }

            _userAccountService.RequestPasswordReset(model.Email, tenantSlug);
            TempData["Message"] = AuthenticationErrorMessages.ForgotPasswordSuccess(
                _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed());

            return RedirectToLogin(tenantSlug);
        }

        public ActionResult ResetPassword(string code, string email)
        {
            ConfigureResetPasswordViewBag();
            return View(new ResetPasswordViewModel { Code = code, Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            ConfigureResetPasswordViewBag();
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!string.Equals(model.Password, model.ConfirmPassword))
            {
                ModelState.AddModelError("ConfirmPassword", "The password and confirmation password do not match.");
                return View(model);
            }

            var policyErrors = _userAccountService.GetPasswordPolicyErrors(model.Password).ToList();
            if (policyErrors.Count > 0)
            {
                foreach (var error in policyErrors)
                {
                    ModelState.AddModelError("Password", error);
                }

                return View(model);
            }

            if (!_authFlowRateLimiter.TryAcquireResetPasswordSubmit(AuthFlowClientAddressHelper.Resolve(HttpContext)))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.ResetPasswordRateLimited());
                return View(model);
            }

            int minutesRemaining;
            if (!_authFlowRateLimiter.IsResetPasswordAllowed(model.Email, model.Code, out minutesRemaining))
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.ResetPasswordTokenLockout(minutesRemaining));
                return View(model);
            }

            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            var resetResult = _userAccountService.ResetPasswordWithTokenDetailed(
                model.Email,
                model.Code,
                model.Password,
                tenantSlug);

            if (resetResult.Succeeded)
            {
                _authFlowRateLimiter.ClearResetPasswordFailures(model.Email, model.Code);
                TempData["Message"] = "Password reset successful.";
                return RedirectToLogin(tenantSlug);
            }

            if (resetResult.FailureReason == PasswordResetFailureReason.PolicyViolation
                && resetResult.PolicyErrors != null)
            {
                foreach (var error in resetResult.PolicyErrors)
                {
                    ModelState.AddModelError("Password", error);
                }

                _authFlowRateLimiter.RecordResetPasswordFailure(model.Email, model.Code);
                return View(model);
            }

            if (resetResult.FailureReason == PasswordResetFailureReason.InvalidOrExpiredToken
                || resetResult.FailureReason == PasswordResetFailureReason.UserNotFound)
            {
                ModelState.AddModelError("", AuthenticationErrorMessages.ResetPasswordInvalidToken());
                return View(model);
            }

            _authFlowRateLimiter.RecordResetPasswordFailure(model.Email, model.Code);
            ModelState.AddModelError("", AuthenticationErrorMessages.ResetPasswordFailure());
            return View(model);
        }

        private ActionResult CompleteLoginAfterCredentials(LoginViewModel model, string returnUrl, string userId, string tenantSlug)
        {
            if (_accountSecurityService != null && _accountSecurityService.UserNeedsLegalConsent(userId))
            {
                StorePendingLegalLogin(model, returnUrl, userId);
                ConfigureLoginViewBag();
                ViewBag.ShowLegalConsentModal = true;
                ConfigureLegalConsentViewBag(returnUrl);
                return View(model);
            }

            return ContinueLoginAfterLegalChecks(model.Email, model.RememberMe, returnUrl, userId, tenantSlug);
        }

        private ActionResult CompleteLoginAfterLegalConsent(string email, bool rememberMe, string returnUrl, string userId, string tenantSlug)
        {
            return ContinueLoginAfterLegalChecks(email, rememberMe, returnUrl, userId, tenantSlug);
        }

        private ActionResult ContinueLoginAfterLegalChecks(string email, bool rememberMe, string returnUrl, string userId, string tenantSlug)
        {
            var safeReturnUrl = ParseReturnPathOrNull(returnUrl);

            if (_accountSecurityService != null && _accountSecurityService.RequiresMfa(userId))
            {
                var user = FindUserById(userId);
                if (user != null && !user.TwoFactorEnabled)
                {
                    Session["ForcedMfaSetupUserId"] = userId;
                    Session["PendingMfaEmail"] = email;
                    Session["PendingMfaRememberMe"] = rememberMe;
                    Session["PendingMfaReturnUrl"] = safeReturnUrl;
                    return RedirectToAction("SetupMfa", new { tenant = tenantSlug });
                }

                Session["PendingMfaUserId"] = userId;
                Session["PendingMfaEmail"] = email;
                Session["PendingMfaRememberMe"] = rememberMe;
                Session["PendingMfaReturnUrl"] = safeReturnUrl;
                return RedirectToAction("VerifyMfa", new { tenant = tenantSlug });
            }

            return IssueAuthCookieAndRedirect(userId, email, rememberMe, safeReturnUrl, tenantSlug);
        }

        private ActionResult IssueAuthCookieAndRedirect(string userId, string email, bool rememberMe, string returnUrl, string tenantSlug)
        {
            var user = FindUserById(userId);
            if (user == null)
            {
                TempData["Error"] = "Your account could not be loaded. Please sign in again.";
                return RedirectToLogin(tenantSlug);
            }

            EnsureLoginAccessToken(user);

            if (_accountSecurityService != null && _accountSecurityService.UserNeedsEmailVerification(userId))
            {
                _accountSecurityService.SendEmailVerificationCode(userId);
                Session["PendingEmailVerificationUserId"] = userId;
                Session["PendingEmailVerificationRememberMe"] = rememberMe;
                Session["PendingEmailVerificationReturnUrl"] = returnUrl;
                return RedirectToAction("VerifyEmail", new { tenant = tenantSlug });
            }

            if (user.RequirePasswordChange)
            {
                CurrentUserExtensions.SetAuthCookie(Response, user, rememberMe, Request.UserAgent, true);
                return RedirectToAction("ChangePassword", new { tenant = tenantSlug });
            }

            CurrentUserExtensions.SetAuthCookie(Response, user, rememberMe, Request.UserAgent);

            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                ImpersonationSessionHelper.TryRestoreAfterLogout(email, Session, _unitOfWork, _auditWriter);
            }

            Session.Remove("PendingMfaEmail");
            Session.Remove("PendingMfaRememberMe");
            Session.Remove("PendingMfaReturnUrl");
            return RedirectToLocal(returnUrl, userId, tenantSlug);
        }

        private void StorePendingLegalLogin(LoginViewModel model, string returnUrl, string userId)
        {
            Session[LegalConsentSession.PendingUserIdSession] = userId;
            Session[LegalConsentSession.PendingStartedTicksSession] = System.DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture);
            Session[LegalConsentSession.PendingEmailSession] = model.Email;
            Session[LegalConsentSession.PendingRememberMeSession] = model.RememberMe;
            var safeReturnUrl = ParseReturnPathOrNull(returnUrl);
            if (!string.IsNullOrWhiteSpace(safeReturnUrl))
            {
                Session[LegalConsentSession.PendingReturnUrlSession] = safeReturnUrl;
            }
            else
            {
                Session.Remove(LegalConsentSession.PendingReturnUrlSession);
            }
        }

        private void ConfigureLegalConsentViewBag(string returnUrl)
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.LegalTermsUrl = string.IsNullOrWhiteSpace(tenantSlug)
                ? Url.Action("Terms", "Home")
                : Url.RouteUrl("Tenant", new { tenant = tenantSlug, controller = "Home", action = "Terms" });
            ViewBag.LegalPrivacyUrl = string.IsNullOrWhiteSpace(tenantSlug)
                ? Url.Action("Privacy", "Home")
                : Url.RouteUrl("Tenant", new { tenant = tenantSlug, controller = "Home", action = "Privacy" });
            ViewBag.ReturnUrl = ParseReturnPathOrNull(returnUrl);
        }

        private string ParseReturnPathOrNull(string returnUrl)
        {
            Uri parsedUri;
            if (!LocalReturnUrlHelper.TryParseLocalReturnUri(returnUrl, Url, out parsedUri))
            {
                return null;
            }

            var path = LocalReturnUrlHelper.FormatReturnPathAndQuery(parsedUri);
            if (LocalReturnUrlHelper.IsDefaultTenantLandingPath(path))
            {
                return null;
            }

            return path;
        }

        private ApplicationUser FindUserById(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return null;
            }

            var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return null;
            }

            return new UserAccountRepository(connectionFactory).FindById(userId);
        }

        private string ResolveLoginTenantSlug()
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            if (!string.IsNullOrWhiteSpace(tenantSlug))
            {
                return tenantSlug;
            }

            var postedTenant = Request.Form["tenantPortal"];
            return string.IsNullOrWhiteSpace(postedTenant) ? null : postedTenant.Trim();
        }

        private ActionResult RedirectToLogin(string tenantSlug)
        {
            return TenantUrlHelper.CreateTenantLoginRedirect(tenantSlug);
        }

        private ActionResult RedirectToLocal(string returnUrl, string userId, string tenantSlug)
        {
            if (IsPlatformAdminUser(userId))
            {
                return PlatformAdminHelper.CreateOrganizationsRedirect();
            }

            var safeReturnUrl = ParseReturnPathOrNull(returnUrl);
            if (!string.IsNullOrWhiteSpace(safeReturnUrl) &&
                PostLoginRedirectHelper.CanAccessReturnPath(_authorizationService, userId, safeReturnUrl))
            {
                return Redirect(safeReturnUrl);
            }

            var destination = PostLoginRedirectHelper.ResolveDefaultDestination(
                _authorizationService,
                userId,
                IsPlatformAdminUser(userId));
            if (!string.IsNullOrWhiteSpace(destination.Area))
            {
                return RedirectToAction(destination.Action, destination.Controller, new { area = destination.Area });
            }

            var organizationSlug = tenantSlug ?? TenantUrlHelper.ResolveOrganizationSlug(_unitOfWork, userId);
            if (TenantUrlHelper.IsValidTenantSlug(organizationSlug))
            {
                return TenantUrlHelper.CreateTenantRedirect(
                    organizationSlug,
                    destination.Controller,
                    destination.Action);
            }

            return RedirectToAction(destination.Action, destination.Controller);
        }

        private void ConfigureLoginViewBag()
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.TenantToken = tenantSlug;
            ViewBag.IsTenantPortal = !string.IsNullOrWhiteSpace(tenantSlug);
            ViewBag.LoginCaptchaEnabled = CaptchaSettings.IsLoginCaptchaEnabled();
            ViewBag.DemoLoginEmail = string.IsNullOrWhiteSpace(tenantSlug)
                ? DemoLoginEmailHelper.PlatformAdminEmail
                : DemoLoginEmailHelper.BuildCompanyAdminEmail(tenantSlug);

            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                return;
            }

            var organization = TenantResolutionHelper.ResolveOrganization(_unitOfWork, tenantSlug);
            if (organization != null)
            {
                ApplyTenantBrandingViewBag(organization);
                ConfigureLicenseBanner(organization.Id);
            }
        }

        private string ValidateLoginCaptcha(string captcha)
        {
            if (!CaptchaSettings.IsLoginCaptchaEnabled())
            {
                return null;
            }

            return CaptchaSessionHelper.ValidateSubmittedCode(Session, captcha, clearOnSuccess: true);
        }

        private void ConfigureLicenseStatusViewBag(LicenseStatus status)
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.TenantToken = tenantSlug;
            ViewBag.LicenseStatus = status;

            Organization organization = null;
            if (!string.IsNullOrWhiteSpace(tenantSlug))
            {
                organization = _unitOfWork.Repository<Organization>().Query()
                    .FirstOrDefault(o => o.Slug != null && o.Slug.Equals(tenantSlug, System.StringComparison.OrdinalIgnoreCase));
            }

            if (organization == null && _organizationScope != null)
            {
                var orgId = _organizationScope.GetCurrentOrganizationId();
                if (orgId.HasValue)
                {
                    organization = _unitOfWork.Repository<Organization>().GetById(orgId.Value);
                }
            }

            if (organization != null)
            {
                ApplyTenantBrandingViewBag(organization);
            }
        }

        private void ApplyTenantBrandingViewBag(Organization organization)
        {
            if (organization == null)
            {
                return;
            }

            ViewBag.OrganizationName = organization.Name;
            ViewBag.OrganizationLogoUrl = OrganizationLogoHelper.GetLogoUrl(Url, organization.LogoPath);
        }

        private void ConfigureLicenseBanner(int organizationId)
        {
            if (_licenseService == null)
            {
                return;
            }

            var license = _licenseService.GetLicenseForOrganization(organizationId);
            var effectiveStatus = _licenseService.GetEffectiveStatus(license);
            if (effectiveStatus == LicenseStatus.Paused)
            {
                ViewBag.LicenseBanner = "Your organization's license is paused. Portal access is suspended until the license is resumed.";
            }
            else if (effectiveStatus == LicenseStatus.Expired)
            {
                ViewBag.LicenseBanner = "Your organization's license has expired. Contact your platform administrator to renew.";
            }
        }

        private Organization ResolveTenantOrganization(string tenantSlug)
        {
            return TenantResolutionHelper.ResolveOrganization(_unitOfWork, tenantSlug);
        }

        private void ConfigureRegisterViewBag(Organization organization, string tenantSlug)
        {
            ViewBag.TenantToken = tenantSlug;
            ViewBag.IsTenantPortal = true;
            ApplyTenantBrandingViewBag(organization);
            ViewBag.PasswordPolicyMessage = PasswordPolicy.GetPolicyMessage();
        }

        private void ConfigureAcceptInviteViewBag(Organization organization, string tenantSlug)
        {
            ViewBag.TenantToken = tenantSlug;
            ViewBag.IsTenantPortal = true;
            ApplyTenantBrandingViewBag(organization);
            ViewBag.PasswordPolicyMessage = PasswordPolicy.GetPolicyMessage();
            ViewBag.OrganizationName = organization == null ? null : organization.Name;
        }

        private void ConfigureForgotPasswordViewBag()
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.TenantToken = tenantSlug;
            ViewBag.IsTenantPortal = !string.IsNullOrWhiteSpace(tenantSlug);

            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                return;
            }

            var organization = ResolveTenantOrganization(tenantSlug);
            if (organization != null)
            {
                ApplyTenantBrandingViewBag(organization);
            }
        }

        private void ConfigureResetPasswordViewBag()
        {
            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            ViewBag.TenantToken = tenantSlug;
            ViewBag.IsTenantPortal = !string.IsNullOrWhiteSpace(tenantSlug);
            ViewBag.PasswordPolicyMessage = PasswordPolicy.GetPolicyMessage();

            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                return;
            }

            var organization = ResolveTenantOrganization(tenantSlug);
            if (organization != null)
            {
                ApplyTenantBrandingViewBag(organization);
            }
        }

        private int? ResolveStaffRoleId(int organizationId)
        {
            var role = _unitOfWork.Repository<Role>().Query()
                .FirstOrDefault(r => r.OrganizationId == organizationId
                    && r.IsActive
                    && r.Name != null
                    && r.Name.Equals("Staff", System.StringComparison.OrdinalIgnoreCase));

            return role == null ? (int?)null : role.Id;
        }

        private bool IsRegistrationAllowed(int organizationId)
        {
            if (_licenseService == null)
            {
                return true;
            }

            var license = _licenseService.GetLicenseForOrganization(organizationId);
            var effectiveStatus = _licenseService.GetEffectiveStatus(license);
            return effectiveStatus == LicenseStatus.Active || effectiveStatus == LicenseStatus.PendingRenewal;
        }

        private static bool IsPlatformAdminUser(string userId)
        {
            return PlatformAdminHelper.IsPlatformAdmin(userId);
        }

        private int? ResolveOrganizationId(string tenantSlug)
        {
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                return null;
            }

            var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
            return TenantResolutionHelper.ResolveOrganizationId(_unitOfWork, connectionFactory, tenantSlug);
        }

        private string GetClientIpAddress()
        {
            var forwarded = Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                var first = forwarded.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(first))
                {
                    return first;
                }
            }

            return Request.UserHostAddress;
        }

        private void PopulateProfileDisplayFields(ProfileViewModel model)
        {
            var userId = User.GetUserId();
            var user = FindUserById(userId);
            if (user == null)
            {
                return;
            }

            model.Email = user.Email;
            model.RoleName = new UserAccountRepository(
                DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>())
                .FindRoleNameByUserId(userId);

            if (user.OrganizationId.HasValue)
            {
                var org = _unitOfWork.Repository<Organization>().GetById(user.OrganizationId.Value);
                model.OrganizationName = org == null ? null : org.Name;
            }
            else
            {
                model.OrganizationName = "Platform";
            }
        }

        [HttpGet]
        public ActionResult DownloadAdminCredentials(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return new HttpStatusCodeResult(400, "Invalid token.");
            }

            var credential = _unitOfWork.Repository<TemporaryCredential>()
                .Query()
                .FirstOrDefault(x => x.Token == token);

            if (credential == null)
            {
                return HttpNotFound();
            }

            if (credential.IsUsed)
            {
                return new HttpStatusCodeResult(410, "This security file has already been downloaded.");
            }

            if (credential.ExpiryDate < DateTime.UtcNow)
            {
                return new HttpStatusCodeResult(410, "This security file has expired.");
            }

            AdminCredentialsViewModel package;
            try
            {
                var decrypted = EncryptionHelper.Decrypt(credential.EncryptedData);
                package = JsonConvert.DeserializeObject<AdminCredentialsViewModel>(decrypted);
            }
            catch (Exception)
            {
                return new HttpStatusCodeResult(500, "Unable to decrypt credential package.");
            }

            if (package == null || string.IsNullOrWhiteSpace(package.AdminPassword))
            {
                return new HttpStatusCodeResult(500, "Invalid credential package.");
            }

            credential.IsUsed = true;
            _unitOfWork.Repository<TemporaryCredential>().Update(credential);
            _unitOfWork.SaveChanges();

            var content = BuildAdminCredentialFileContent(package);
            var bytes = Encoding.UTF8.GetBytes(content);
            var fileName = SanitizeCredentialFileName(package.CompanyName) + "_Admin_Credentials.txt";
            return File(bytes, "text/plain", fileName);
        }

        private static string BuildAdminCredentialFileContent(AdminCredentialsViewModel package)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==============================================================");
            sb.AppendLine("ASSET MANAGEMENT - SECURE ADMIN CREDENTIALS");
            sb.AppendLine("==============================================================");
            sb.AppendLine();
            sb.AppendLine("Company Name      : " + (package.CompanyName ?? string.Empty));
            sb.AppendLine("Login URL         : " + (package.CompanyUrl ?? string.Empty));
            sb.AppendLine("Admin Username    : " + (package.AdminUsername ?? string.Empty));
            sb.AppendLine("Temporary Password: " + (package.AdminPassword ?? string.Empty));
            sb.AppendLine();
            sb.AppendLine("IMPORTANT");
            sb.AppendLine("- Store this file securely and do not share it broadly.");
            sb.AppendLine("- The company admin must change this password on first sign-in.");
            sb.AppendLine("- This download link is single-use and expires after one hour.");
            sb.AppendLine("- Delete this file after the admin has signed in successfully.");
            sb.AppendLine();
            sb.AppendLine("Generated (UTC)   : " + DateTime.UtcNow.ToString("u"));
            sb.AppendLine("==============================================================");
            return sb.ToString();
        }

        private static string SanitizeCredentialFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Organization";
            }

            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "Organization" : cleaned.Trim();
        }
    }
}
