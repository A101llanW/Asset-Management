using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Security;
using AssetSuite.Web.Helpers;
using AssetManagement.Domain.Entities;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetSuite.Web.Infrastructure;
using AssetSuite.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetSuite.Web.Controllers;

[Route("[controller]/[action]")]
[Route("{tenant}/[controller]/[action]")]
public class AccountController : Controller
{
    private readonly IUserAccountService _userAccountService;
    private readonly IAccountSecurityService _accountSecurityService;
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly AssetSuiteAuthService _authService;
    private readonly IUnitOfWork _unitOfWork;

    public AccountController(
        IUserAccountService userAccountService,
        IAccountSecurityService accountSecurityService,
        ISqlConnectionFactory connectionFactory,
        AssetSuiteAuthService authService,
        IUnitOfWork unitOfWork)
    {
        _userAccountService = userAccountService;
        _accountSecurityService = accountSecurityService;
        _connectionFactory = connectionFactory;
        _authService = authService;
        _unitOfWork = unitOfWork;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl, string? tenant)
    {
        tenant = ResolveTenantSlug(tenant);
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            ViewBag.IsTenantPortal = true;
        }

        ViewBag.ReturnUrl = returnUrl;
        ViewBag.TenantToken = tenant;
        ViewBag.ShowLoginBackground = true;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl, string? tenant)
    {
        tenant = ResolveTenantSlug(tenant);
        ViewBag.ReturnUrl = returnUrl;
        ViewBag.TenantToken = tenant;
        ViewBag.ShowLoginBackground = true;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        int? organizationId = null;
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            organizationId = new UserAccountRepository(_connectionFactory).FindOrganizationIdBySlug(tenant);
        }

        if (_accountSecurityService.IsLoginIpRateLimited(clientIp))
        {
            ModelState.AddModelError(string.Empty, AuthenticationErrorMessages.LoginIpRateLimited());
            return View(model);
        }

        if (_accountSecurityService.IsAccountLocked(model.Email, organizationId))
        {
            ModelState.AddModelError(string.Empty, AuthenticationErrorMessages.LoginAccountLocked(15));
            return View(model);
        }

        if (!_userAccountService.ValidateCredentials(model.Email, model.Password, tenant, out var userId))
        {
            _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, false, organizationId, "Invalid credentials");
            var remaining = _accountSecurityService.GetRemainingLoginAttempts(model.Email, organizationId);
            ModelState.AddModelError(string.Empty, AuthenticationErrorMessages.LoginFailure(model.Email, tenant, remaining));
            return View(model);
        }

        _accountSecurityService.RecordLoginAttempt(model.Email, clientIp, true, organizationId, null);
        _accountSecurityService.ClearFailedLoginAttempts(model.Email, organizationId);

        var user = new UserAccountRepository(_connectionFactory).FindById(userId);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Your account could not be loaded.");
            return View(model);
        }

        EnsureLoginAccessToken(user);

        if (user.RequirePasswordChange)
        {
            await _authService.SignInAsync(HttpContext, user, model.RememberMe, true);
            return RedirectToTenantAction("ChangePassword", tenant);
        }

        await _authService.SignInAsync(HttpContext, user, model.RememberMe, false);
        return RedirectAfterLogin(returnUrl, user, tenant);
    }

    [Authorize]
    [HttpGet]
    public IActionResult Landing(string? tenant)
    {
        return RedirectToTenantAction("Privacy", tenant);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(string? tenant)
    {
        await _authService.SignOutAsync(HttpContext);
        return RedirectToTenantAction("Login", tenant);
    }

    private void EnsureLoginAccessToken(ApplicationUser user)
    {
        if (user == null || !string.IsNullOrWhiteSpace(user.AccessToken))
        {
            return;
        }

        user.AccessToken = SecurePasswordGenerator.GenerateAccessToken();
        new UserAccountRepository(_connectionFactory).Update(user);
    }

    private string? ResolveTenantSlug(string? tenant) =>
        !string.IsNullOrWhiteSpace(tenant)
            ? tenant
            : RouteData.Values.TryGetValue("tenant", out var routeTenant)
                ? routeTenant as string
                : null;

    private IActionResult RedirectAfterLogin(string? returnUrl, ApplicationUser user, string? tenant)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        if (!string.IsNullOrWhiteSpace(tenant))
        {
            return Redirect("/" + tenant + "/Home/Privacy");
        }

        return RedirectToAction("Privacy", "Home");
    }

    private IActionResult RedirectToTenantAction(string action, string? tenant)
    {
        if (!string.IsNullOrWhiteSpace(tenant))
        {
            return Redirect("/" + tenant + "/Account/" + action);
        }

        return RedirectToAction(action);
    }
}
