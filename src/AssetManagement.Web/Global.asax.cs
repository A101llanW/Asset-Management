using System;
using System.Configuration;
using System.Diagnostics;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Web.Security;
using System.Security.Principal;
using AssetManagement.Application.Contracts;
using AssetManagement.Application.Contracts.Organizations;
using AssetManagement.Application.Contracts.Security;
using AssetManagement.Application.Security;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Web.App_Start;
using AssetManagement.Web.Helpers;
using AssetManagement.Web.Security;

namespace AssetManagement.Web
{
    public class MvcApplication : System.Web.HttpApplication
    {

        protected void Application_Start()
        {
            ConfigureDevelopmentTraceListeners();
            MachineKeyDeploymentValidator.WarnIfMissingInProduction();
            DatabaseConfig.Configure();
            AutofacConfig.Register();
            ValidateSmtpConfigurationAtStartup();
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        private static void ConfigureDevelopmentTraceListeners()
        {
            if (!DeploymentSecuritySettings.MfaAllowAnyCode)
            {
                return;
            }

            Trace.AutoFlush = true;

            var hasDefaultListener = false;
            var hasConsoleListener = false;
            foreach (TraceListener listener in Trace.Listeners)
            {
                if (listener is DefaultTraceListener)
                {
                    hasDefaultListener = true;
                }

                if (listener is ConsoleTraceListener)
                {
                    hasConsoleListener = true;
                }
            }

            if (!hasDefaultListener)
            {
                Trace.Listeners.Add(new DefaultTraceListener());
            }

            if (!hasConsoleListener)
            {
                Trace.Listeners.Add(new ConsoleTraceListener());
            }
        }

        private static void ValidateSmtpConfigurationAtStartup()
        {
            try
            {
                var emailService = DependencyResolver.Current.GetService<IEmailService>();
                var message = SmtpStartupValidator.ValidateForStartup(emailService);
                if (string.IsNullOrWhiteSpace(message))
                {
                    return;
                }

                Trace.TraceError("SMTP configuration warning: " + message);
            }
            catch (Exception ex)
            {
                Trace.TraceError("SMTP startup validation failed: " + ex.Message);
            }
        }

        protected void Application_PostAuthenticateRequest(object sender, EventArgs e)
        {
            if (Request == null)
            {
                return;
            }

            var authCookie = Request.Cookies[FormsAuthentication.FormsCookieName];
            if (authCookie == null || string.IsNullOrWhiteSpace(authCookie.Value))
            {
                return;
            }

            if (IsPublicAuthPath(Request.Path))
            {
                return;
            }

            FormsAuthenticationTicket ticket;
            try
            {
                ticket = FormsAuthentication.Decrypt(authCookie.Value);
            }
            catch
            {
                return;
            }

            if (ticket == null || string.IsNullOrWhiteSpace(ticket.UserData))
            {
                return;
            }

            string userId;
            int? organizationId;
            string accessToken;
            string uaHash;
            bool requirePasswordChange;
            if (!AuthTicketHelper.TryParseUserData(ticket.UserData, out userId, out organizationId, out accessToken, out uaHash, out requirePasswordChange))
            {
                SetupPrincipal(ticket, userId);
                return;
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                var connectionFactory = DependencyResolver.Current.GetService<ISqlConnectionFactory>();
                if (connectionFactory == null)
                {
                    InvalidateSession("session_invalid");
                    return;
                }

                var repository = new UserAccountRepository(connectionFactory);
                if (!repository.ValidateAccessToken(userId, organizationId, accessToken))
                {
                    InvalidateSession("session_invalid");
                    return;
                }

                if (!ValidateUserAgentFingerprint(uaHash))
                {
                    InvalidateSession("session_hijack");
                    return;
                }

                var accountSecurity = DependencyResolver.Current.GetService<IAccountSecurityService>();
                if (accountSecurity != null
                    && !string.IsNullOrWhiteSpace(ticket.Name)
                    && accountSecurity.IsAccountLocked(ticket.Name, organizationId))
                {
                    InvalidateSession("account_locked");
                    return;
                }
            }

            if (requirePasswordChange && !IsPasswordChangeExemptPath(Request.Path))
            {
                RedirectToRequiredPasswordChange(organizationId);
                return;
            }

            if (organizationId.HasValue)
            {
                Context.Items[TenantContextKeys.AuthenticatedOrganizationId] = organizationId.Value;
            }

            SetupPrincipal(ticket, userId);
        }

        private void SetupPrincipal(FormsAuthenticationTicket ticket, string userId)
        {
            if (Context == null || ticket == null)
            {
                return;
            }

            var identity = new FormsIdentity(ticket);
            var principal = new GenericPrincipal(identity, new string[0]);
            Context.User = principal;
            System.Threading.Thread.CurrentPrincipal = principal;
        }

        private static bool IsPublicAuthPath(string requestPath)
        {
            if (string.IsNullOrEmpty(requestPath))
            {
                return false;
            }

            var path = requestPath.ToLowerInvariant();
            return path.Contains("/account/login")
                || path.Contains("/account/register")
                || path.Contains("/account/forgotpassword")
                || path.Contains("/account/resetpassword")
                || path.Contains("/account/verifymfa")
                || path.Contains("/account/setupmfa")
                || path.Contains("/account/verifyemail")
                || path.Contains("/account/confirmlegalconsent")
                || path.Contains("/account/changepassword")
                || path.Contains("/account/downloadadmincredentials")
                || path.Contains("/home/privacy")
                || path.Contains("/home/terms")
                || path.Contains("/content/")
                || path.Contains("/scripts/");
        }

        private bool ValidateUserAgentFingerprint(string storedUaHash)
        {
            if (string.IsNullOrEmpty(storedUaHash))
            {
                return true;
            }

            var currentUaHash = UserAgentFingerprint.Compute(Request.UserAgent);
            return string.Equals(currentUaHash, storedUaHash, StringComparison.Ordinal);
        }

        private static bool IsPasswordChangeExemptPath(string requestPath)
        {
            if (string.IsNullOrEmpty(requestPath))
            {
                return false;
            }

            var path = requestPath.ToLowerInvariant();
            return path.Contains("/account/changepassword")
                || path.Contains("/account/logoff")
                || path.Contains("/content/")
                || path.Contains("/scripts/")
                || path.Contains("/captcha/");
        }

        private void RedirectToRequiredPasswordChange(int? organizationId)
        {
            string redirectPath;
            if (organizationId.HasValue)
            {
                var unitOfWork = DependencyResolver.Current.GetService<IUnitOfWork>();
                var slug = TenantUrlHelper.ResolveOrganizationSlug(unitOfWork, organizationId.Value);
                redirectPath = TenantUrlHelper.IsValidTenantSlug(slug)
                    ? TenantUrlHelper.BuildTenantPath(slug, "Account", "ChangePassword")
                    : "~/Account/ChangePassword";
            }
            else
            {
                redirectPath = "~/Account/ChangePassword";
            }

            Response.Redirect(VirtualPathUtility.ToAbsolute(redirectPath), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void InvalidateSession(string reason)
        {
            AuthSessionHelper.SignOut(new HttpContextWrapper(Context));
            var loginUrl = FormsAuthentication.LoginUrl;
            if (string.IsNullOrEmpty(loginUrl))
            {
                loginUrl = "~/Account/Login";
            }

            var separator = loginUrl.Contains("?") ? "&" : "?";
            Response.Redirect(loginUrl + separator + "reason=" + reason, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        protected void Application_BeginRequest()
        {
            if (HttpsRedirectHelper.ShouldRedirectToHttps(Request))
            {
                var host = Request.Url.Host;
                var pathAndQuery = Request.Url.PathAndQuery;
                Response.Redirect("https://" + host + pathAndQuery, true);
                return;
            }

            // Scheduled and outbox work is owned by the external worker process.
        }

    }
}
