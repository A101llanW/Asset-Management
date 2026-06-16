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
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Models;
using AssetManagement.Web.Security;

namespace AssetManagement.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserAccountService _userAccountService;
        private readonly IAuthorizationService _authorizationService;
        private readonly ISsoAuthenticationProvider _ssoProvider;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IAuditWriter _auditWriter;
        private readonly IOrganizationScopeService _organizationScope;
        private readonly IOrganizationLicenseService _licenseService;
        private readonly IAccountSecurityService _accountSecurityService;

        public AccountController()
        {
            _userAccountService = DependencyResolver.Current.GetService<IUserAccountService>();
            _authorizationService = DependencyResolver.Current.GetService<IAuthorizationService>();
            _ssoProvider = DependencyResolver.Current.GetService<ISsoAuthenticationProvider>();
            _unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
            _auditWriter = DependencyResolver.Current.GetService<IAuditWriter>();
            _organizationScope = DependencyResolver.Current.GetService<IOrganizationScopeService>();
            _licenseService = DependencyResolver.Current.GetService<IOrganizationLicenseService>();
            _accountSecurityService = DependencyResolver.Current.GetService<IAccountSecurityService>();
        }

        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
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
        public ActionResult ExternalLogin(string token, string returnUrl)
        {
            if (_ssoProvider == null || !_ssoProvider.IsEnabled)
            {
                var message = _ssoProvider == null
                    ? "SSO is not available."
                    : _ssoProvider.TryAuthenticate(token).Message;
                TempData["Error"] = message;
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            var result = _ssoProvider.TryAuthenticate(token);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.UserId))
            {
                TempData["Error"] = result.Message ?? "SSO sign-in failed.";
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            var ticketUser = new ApplicationUser { Id = result.UserId };
            CurrentUserExtensions.SetAuthCookie(Response, ticketUser, false);
            return RedirectToLocal(returnUrl, result.UserId, TenantUrlHelper.GetTenantToken(RouteData));
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
                ModelState.AddModelError("", "Too many failed login attempts from your location. Please wait 15 minutes before trying again.");
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
                ModelState.AddModelError("", "Account is locked. Please try again in " + minutesRemaining + " minutes.");
                return View(model);
            }

            string userId;
            if (!_userAccountService.ValidateCredentials(model.Email, model.Password, tenantSlug, out userId))
            {
                _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, false, organizationId, "Invalid credentials");
                var remaining = _accountSecurityService.GetRemainingLoginAttempts(model.Email, organizationId);
                ModelState.AddModelError("", BuildLoginFailureMessage(model.Email, tenantSlug, remaining));
                return View(model);
            }

            _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, true, organizationId, null);
            _accountSecurityService.ClearFailedLoginAttempts(model.Email, organizationId);

            return CompleteLoginAfterCredentials(model, returnUrl, userId, tenantSlug);
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
                ModelState.AddModelError("", "Current password is incorrect or the new password could not be saved.");
                return View(model);
            }

            TempData["Message"] = "Your password has been updated successfully.";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmLegalConsent(bool acceptLegalTerms, string returnUrl)
        {
            ConfigureLoginViewBag();
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
                ConfigureLegalConsentViewBag(returnUrl);
                return View("Login", new LoginViewModel { Email = Session[LegalConsentSession.PendingEmailSession] as string });
            }

            _accountSecurityService.RecordLegalAcceptance(userId);

            var rememberMe = Session[LegalConsentSession.PendingRememberMeSession] as bool? ?? false;
            var email = Session[LegalConsentSession.PendingEmailSession] as string;
            var pendingReturnUrl = Session[LegalConsentSession.PendingReturnUrlSession] as string;
            LegalConsentSession.Clear(Session);

            var tenantSlug = TenantUrlHelper.GetTenantToken(RouteData);
            return CompleteLoginAfterLegalConsent(email, rememberMe, pendingReturnUrl ?? returnUrl, userId, tenantSlug);
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

            if (!_accountSecurityService.ValidateMfaCode(userId, code))
            {
                ModelState.AddModelError("", "Invalid verification code. Please try again.");
                ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
                ViewBag.Email = FindUserById(userId) != null ? FindUserById(userId).Email : null;
                ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
                return View("SetupMfa");
            }

            _accountSecurityService.EnableMfa(userId, method);
            Session.Remove("ForcedMfaSetupUserId");
            return IssueAuthCookieAndRedirect(
                userId,
                Session["PendingMfaEmail"] as string,
                Session["PendingMfaRememberMe"] as bool? ?? false,
                Session["PendingMfaReturnUrl"] as string,
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

            if (!_accountSecurityService.SendMfaCode(userId))
            {
                return Json(new { success = false, message = "Could not send verification code." });
            }

            return Json(new { success = true, message = "Verification code sent. In development, check debug/trace output." });
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
            if (!_accountSecurityService.SendMfaCode(userId))
            {
                ViewBag.MfaSendError = "Could not send a verification code. Use Resend below or check SMTP configuration.";
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

            if (!_accountSecurityService.ValidateMfaCode(userId, code))
            {
                ModelState.AddModelError("", "Invalid or expired verification code.");
                ViewBag.TenantToken = TenantUrlHelper.GetTenantToken(RouteData);
                ViewBag.EmailHint = _accountSecurityService.MaskEmail(FindUserById(userId) != null ? FindUserById(userId).Email : null);
                ViewBag.MfaDevMode = _accountSecurityService != null && _accountSecurityService.IsMfaCodeValidationRelaxed();
                return View("VerifyMfa");
            }

            _accountSecurityService.ClearMfaCode(userId);
            Session.Remove("PendingMfaUserId");
            return IssueAuthCookieAndRedirect(
                userId,
                Session["PendingMfaEmail"] as string,
                Session["PendingMfaRememberMe"] as bool? ?? false,
                Session["PendingMfaReturnUrl"] as string,
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

            if (!_accountSecurityService.SendMfaCode(userId))
            {
                return Json(new { success = false, message = "Could not resend verification code." });
            }

            return Json(new { success = true, message = "Verification code resent." });
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

            System.Web.Security.FormsAuthentication.SignOut();
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
                ModelState.AddModelError("", "Registration is unavailable because the Staff role is not configured.");
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
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error);
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
                TempData["Message"] = "If that email is registered, a reset link has been sent.";
                return RedirectToLogin(tenantSlug);
            }

            if (_accountSecurityService != null)
            {
                _accountSecurityService.RecordForgotPasswordAttempt(clientIp, model.Email, organizationId);
            }

            _userAccountService.RequestPasswordReset(model.Email, tenantSlug);
            TempData["Message"] = "If that email is registered, a reset link has been sent.";

            return RedirectToLogin(tenantSlug);
        }

        public ActionResult ResetPassword(string code, string email)
        {
            return View(new ResetPasswordViewModel { Code = code, Email = email });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!string.Equals(model.Password, model.ConfirmPassword))
            {
                ModelState.AddModelError("ConfirmPassword", "The password and confirmation password do not match.");
                return View(model);
            }

            if (_userAccountService.ResetPasswordWithToken(model.Email, model.Code, model.Password))
            {
                TempData["Message"] = "Password reset successful.";
                return RedirectToLogin(TenantUrlHelper.GetTenantToken(RouteData));
            }

            ModelState.AddModelError("", "Password reset failed. Ensure the password meets complexity requirements.");
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
            if (_accountSecurityService != null && _accountSecurityService.RequiresPrivilegedMfa(userId))
            {
                var user = FindUserById(userId);
                if (user != null && !user.TwoFactorEnabled)
                {
                    Session["ForcedMfaSetupUserId"] = userId;
                    Session["PendingMfaEmail"] = email;
                    Session["PendingMfaRememberMe"] = rememberMe;
                    Session["PendingMfaReturnUrl"] = returnUrl;
                    return RedirectToAction("SetupMfa", new { tenant = tenantSlug });
                }

                Session["PendingMfaUserId"] = userId;
                Session["PendingMfaEmail"] = email;
                Session["PendingMfaRememberMe"] = rememberMe;
                Session["PendingMfaReturnUrl"] = returnUrl;
                return RedirectToAction("VerifyMfa", new { tenant = tenantSlug });
            }

            return IssueAuthCookieAndRedirect(userId, email, rememberMe, returnUrl, tenantSlug);
        }

        private ActionResult IssueAuthCookieAndRedirect(string userId, string email, bool rememberMe, string returnUrl, string tenantSlug)
        {
            var ticketUser = new ApplicationUser { Id = userId, Email = email };
            CurrentUserExtensions.SetAuthCookie(Response, ticketUser, rememberMe);

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
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                Session[LegalConsentSession.PendingReturnUrlSession] = returnUrl;
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
            ViewBag.ReturnUrl = returnUrl;
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

            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var destination = ResolveDefaultDestination(userId);
            if (!string.IsNullOrWhiteSpace(destination.Area))
            {
                return RedirectToDestination(destination);
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

            var organization = _unitOfWork.Repository<Organization>().Query()
                .FirstOrDefault(o => o.Slug != null && o.Slug.Equals(tenantSlug, System.StringComparison.OrdinalIgnoreCase));
            if (organization != null)
            {
                ViewBag.OrganizationName = organization.Name;
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
                ViewBag.OrganizationName = organization.Name;
            }
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
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                return null;
            }

            return _unitOfWork.Repository<Organization>().Query()
                .FirstOrDefault(o => o.IsActive
                    && o.Slug != null
                    && o.Slug.Equals(tenantSlug.Trim(), System.StringComparison.OrdinalIgnoreCase));
        }

        private void ConfigureRegisterViewBag(Organization organization, string tenantSlug)
        {
            ViewBag.TenantToken = tenantSlug;
            ViewBag.IsTenantPortal = true;
            ViewBag.OrganizationName = organization == null ? null : organization.Name;
            ViewBag.PasswordPolicyMessage = PasswordPolicy.GetPolicyMessage();
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
                ViewBag.OrganizationName = organization.Name;
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

        private ActionResult RedirectToDestination(RouteTarget destination)
        {
            if (!string.IsNullOrWhiteSpace(destination.Area))
            {
                if (string.Equals(destination.Area, "Platform", System.StringComparison.OrdinalIgnoreCase))
                {
                    return PlatformAdminHelper.CreateOrganizationsRedirect();
                }

                return RedirectToAction(destination.Action, destination.Controller, new { area = destination.Area });
            }

            return RedirectToAction(destination.Action, destination.Controller);
        }

        private RouteTarget ResolveDefaultDestination(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new RouteTarget("Dashboard", "Index");
            }

            if (IsPlatformAdminUser(userId))
            {
                return new RouteTarget("Organizations", "Index", "Platform");
            }

            var candidates = new[]
            {
                new { Permission = "Reports.View", Controller = "Dashboard", Action = "Index" },
                new { Permission = "Assets.View", Controller = "Assets", Action = "Index" },
                new { Permission = "Incidents.View", Controller = "Incidents", Action = "Index" },
                new { Permission = "Claims.View", Controller = "Claims", Action = "Index" },
                new { Permission = "Departments.View", Controller = "Departments", Action = "Index" },
                new { Permission = "Suppliers.View", Controller = "Suppliers", Action = "Index" },
                new { Permission = "Users.View", Controller = "Users", Action = "Index" },
                new { Permission = "Roles.View", Controller = "Roles", Action = "Index" },
                new { Permission = "AuditLogs.View", Controller = "AuditLogs", Action = "Index" },
                new { Permission = "Settings.Manage", Controller = "Settings", Action = "Index" }
            };

            foreach (var candidate in candidates)
            {
                if (_authorizationService.HasPermission(userId, candidate.Permission))
                {
                    return new RouteTarget(candidate.Controller, candidate.Action);
                }
            }

            return new RouteTarget("Dashboard", "Index");
        }

        private static string BuildInvalidLoginMessage(int remainingAttempts)
        {
            if (remainingAttempts > 1)
            {
                return "Invalid login attempt. " + remainingAttempts + " attempts remaining.";
            }

            if (remainingAttempts == 1)
            {
                return "Invalid login attempt. 1 attempt remaining before account lockout.";
            }

            return "Invalid login attempt. Account is now locked for 30 minutes.";
        }

        private string BuildLoginFailureMessage(string email, string tenantSlug, int remainingAttempts)
        {
            var message = BuildInvalidLoginMessage(remainingAttempts);
            if (!string.IsNullOrWhiteSpace(tenantSlug) || string.IsNullOrWhiteSpace(email))
            {
                return message;
            }

            if (!DemoLoginEmailHelper.IsPlatformAdminEmail(email))
            {
                return message;
            }

            var connectionFactory = DependencyResolver.Current.GetService<Infrastructure.Persistence.ISqlConnectionFactory>();
            if (connectionFactory == null)
            {
                return message;
            }

            var users = new UserAccountRepository(connectionFactory);
            if (users.FindPlatformAdminByEmail(email.Trim()) != null)
            {
                return message + " Check that the password is P@ssw0rd! for demo accounts.";
            }

            return message
                + " No platform administrator account exists yet. Run .\\unlock-logins.ps1 (or .\\initialize-database.ps1), then use superadmin@asset.local / P@ssw0rd! here."
                + " Company admins must use their organization portal (for example /nanosoft/Account/Login with nanosoft@asset.local).";
        }

        private int? ResolveOrganizationId(string tenantSlug)
        {
            if (string.IsNullOrWhiteSpace(tenantSlug))
            {
                return null;
            }

            var org = _unitOfWork.Repository<Organization>().Query()
                .FirstOrDefault(o => o.Slug != null && o.Slug.Equals(tenantSlug, System.StringComparison.OrdinalIgnoreCase));
            return org == null ? (int?)null : org.Id;
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

        private sealed class RouteTarget
        {
            public RouteTarget(string controller, string action, string area = null)
            {
                Controller = controller;
                Action = action;
                Area = area;
            }

            public string Controller { get; private set; }

            public string Action { get; private set; }

            public string Area { get; private set; }
        }
    }
}
