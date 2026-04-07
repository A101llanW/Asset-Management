using System;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using AssetManagement.Infrastructure.Persistence;
using AssetManagement.Infrastructure.Repositories;
using AssetManagement.Infrastructure.Services;
using AssetManagement.Web.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;

namespace AssetManagement.Web.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get => _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            private set => _signInManager = value;
        }

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set => _userManager = value;
        }

        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        public ActionResult Landing()
        {
            var userId = User.Identity.GetUserId();
            var destination = ResolveDefaultDestination(userId);
            return RedirectToAction(destination.Action, destination.Controller);
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await SignInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    var user = await UserManager.FindByEmailAsync(model.Email);
                    if (user == null)
                    {
                        AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                        ModelState.AddModelError("", "Unable to resolve user profile after login.");
                        return View(model);
                    }

                    return RedirectToLocal(returnUrl, user.Id);
                default:
                    ModelState.AddModelError("", "Invalid login attempt.");
                    return View(model);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Login", "Account");
        }

        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            TempData["Message"] = "Password reset flow placeholder. Integrate email sender for production.";
            return RedirectToAction("Login");
        }

        [AllowAnonymous]
        public ActionResult ResetPassword(string code)
        {
            return View(new ResetPasswordViewModel { Code = code });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var result = await UserManager.ResetPasswordAsync(user.Id, model.Code, model.Password);
            if (result.Succeeded)
            {
                TempData["Message"] = "Password reset successful.";
                return RedirectToAction("Login");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }

            return View(model);
        }

        private ActionResult RedirectToLocal(string returnUrl, string userId)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            var destination = ResolveDefaultDestination(userId);
            return RedirectToAction(destination.Action, destination.Controller);
        }

        private static RouteTarget ResolveDefaultDestination(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new RouteTarget("Dashboard", "Index");
            }

            using (var context = new AssetManagementDbContext())
            using (var unitOfWork = new UnitOfWork(context))
            {
                var authorizationService = new AuthorizationService(unitOfWork);
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
                    if (authorizationService.HasPermission(userId, candidate.Permission))
                    {
                        return new RouteTarget(candidate.Controller, candidate.Action);
                    }
                }
            }

            return new RouteTarget("Dashboard", "Index");
        }

        private IAuthenticationManager AuthenticationManager => HttpContext.GetOwinContext().Authentication;

        private sealed class RouteTarget
        {
            public RouteTarget(string controller, string action)
            {
                Controller = controller;
                Action = action;
            }

            public string Controller { get; }

            public string Action { get; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _userManager?.Dispose();
                _signInManager?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
