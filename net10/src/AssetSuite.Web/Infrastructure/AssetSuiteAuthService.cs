using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using AssetSuite.Web.Helpers;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;

namespace AssetSuite.Web.Infrastructure;

public sealed class AssetSuiteAuthService
{
    private readonly IConfiguration _configuration;

    public AssetSuiteAuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SignInAsync(HttpContext httpContext, ApplicationUser user, bool rememberMe, bool requirePasswordChange)
    {
        if (user == null || string.IsNullOrWhiteSpace(user.Id))
        {
            return;
        }

        var uaHash = UserAgentFingerprint.Compute(httpContext.Request.Headers.UserAgent.ToString());
        var userData = AuthTicketHelper.BuildUserData(
            user.Id,
            user.OrganizationId,
            user.AccessToken,
            uaHash,
            requirePasswordChange);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.Email) ? user.Id : user.Email),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(FormsAuthHelper.UserIdClaimType, user.Id),
            new(FormsAuthHelper.UserDataClaimType, userData)
        };

        if (user.OrganizationId.HasValue)
        {
            claims.Add(new Claim("organization_id", user.OrganizationId.Value.ToString()));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var timeoutMinutes = _configuration.GetValue("AssetSuite:FormsTimeoutMinutes", 120);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddMinutes(timeoutMinutes) : null
            });

        if (user.OrganizationId.HasValue)
        {
            httpContext.Items["AuthenticatedOrganizationId"] = user.OrganizationId.Value;
        }
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
