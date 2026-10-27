using AssetManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AssetSuite.Web.Infrastructure;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddAssetSuiteAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var cookieName = configuration["AssetSuite:CookieName"] ?? ".ASPXAUTH";
        var loginPath = configuration["AssetSuite:LoginPath"] ?? "/Account/Login";
        var requireSecure = configuration.GetValue("AssetSuite:RequireSecureCookies", false);

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = cookieName;
                options.LoginPath = loginPath;
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = requireSecure
                    ? CookieSecurePolicy.Always
                    : CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Events.OnValidatePrincipal = context =>
                {
                    var userData = context.Principal?.FindFirst(FormsAuthHelper.UserDataClaimType)?.Value;
                    if (!string.IsNullOrWhiteSpace(userData))
                    {
                        context.HttpContext.Items["AssetSuiteUserData"] = userData;
                    }

                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization();
        return services;
    }
}
