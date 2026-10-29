using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using AssetManagement.Application.Contracts;
using AssetManagement.Domain.Entities;
using AssetManagement.Web.Filters;
using AssetManagement.Web.Helpers;
using Moq;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// t770u: a signed-in user who types another company's URL is sent to the same page on their own company's URL,
    /// on EVERY page (whitelisted Account/Home/Captcha/Dashboard actions included), so no other company's name, logo,
    /// licence banner or data is ever built for them. Log off and child actions pass through.
    /// </summary>
    [TestFixture]
    public class ForeignTenantRedirectTests
    {
        private IDependencyResolver _previous;

        [SetUp]
        public void SetUp()
        {
            _previous = DependencyResolver.Current;
            var orgs = new Mock<IRepository<Organization>>();
            orgs.Setup(r => r.GetById(It.Is<object>(o => Convert.ToInt32(o) == 13)))
                .Returns(new Organization { Id = 13, Slug = "own-co", Name = "Own Co", IsActive = true });
            var uow = new Mock<IUnitOfWork>();
            uow.Setup(u => u.Repository<Organization>()).Returns(orgs.Object);
            var resolver = new Mock<IDependencyResolver>();
            resolver.Setup(r => r.GetService(typeof(IUnitOfWork))).Returns(uow.Object);
            resolver.Setup(r => r.GetServices(It.IsAny<Type>())).Returns(Enumerable.Empty<object>());
            DependencyResolver.SetResolver(resolver.Object);
        }

        [TearDown]
        public void TearDown()
        {
            DependencyResolver.SetResolver(_previous);
        }

        private static ActionExecutingContext Context(string controller, string action, object id = null, bool child = false, int? ownOrg = 13)
        {
            var items = new Hashtable();
            if (ownOrg.HasValue)
            {
                items[TenantContextKeys.AuthenticatedOrganizationId] = ownOrg.Value;
            }

            var request = new Mock<HttpRequestBase>();
            request.SetupGet(r => r.QueryString).Returns(new System.Collections.Specialized.NameValueCollection());
            request.SetupGet(r => r.Url).Returns(new Uri("http://localhost:8080/other-co/" + controller + "/" + action));
            var http = new Mock<HttpContextBase>();
            http.SetupGet(h => h.Items).Returns(items);
            http.SetupGet(h => h.Request).Returns(request.Object);

            var routeData = new RouteData();
            routeData.Values["tenant"] = "other-co";
            routeData.Values["controller"] = controller;
            routeData.Values["action"] = action;
            if (id != null)
            {
                routeData.Values["id"] = id;
            }

            if (child)
            {
                routeData.DataTokens["ParentActionViewContext"] = new ViewContext();
            }

            var controllerDescriptor = new Mock<ControllerDescriptor>();
            controllerDescriptor.SetupGet(d => d.ControllerName).Returns(controller);
            var actionDescriptor = new Mock<ActionDescriptor>();
            actionDescriptor.SetupGet(d => d.ActionName).Returns(action);
            actionDescriptor.SetupGet(d => d.ControllerDescriptor).Returns(controllerDescriptor.Object);

            return new ActionExecutingContext
            {
                HttpContext = http.Object,
                RouteData = routeData,
                ActionDescriptor = actionDescriptor.Object,
                ActionParameters = new Dictionary<string, object>()
            };
        }

        [TestCase("Account", "Login")]
        [TestCase("Account", "Profile")]
        [TestCase("Account", "ForgotPassword")]
        [TestCase("Account", "ResetPassword")]
        [TestCase("Account", "Register")]
        [TestCase("Account", "AcceptInvite")]
        [TestCase("Account", "LicenseSuspended")]
        [TestCase("Account", "LicenseExpired")]
        [TestCase("Account", "ChangePassword")]
        [TestCase("Home", "Privacy")]
        [TestCase("Home", "Terms")]
        [TestCase("Dashboard", "GetPendingRequests")]
        [TestCase("Dashboard", "Index")]
        [TestCase("HelpVideos", "Index")]
        [TestCase("Reports", "Index")]
        [TestCase("AssetLabels", "Index")]
        [TestCase("Captcha", "Generate")]
        public void SignedInOnAnotherCompanysUrl_GoesToTheSamePageOnOwnCompany(string controller, string action)
        {
            var result = TenantFilterAttribute.BuildOwnTenantRedirect(Context(controller, action)) as RedirectResult;
            Assert.IsNotNull(result, "redirected");
            StringAssert.Contains("/own-co/" + controller + "/" + action, result.Url);
            StringAssert.DoesNotContain("other-co", result.Url);
        }

        [Test]
        public void RedirectKeepsTheId()
        {
            var result = TenantFilterAttribute.BuildOwnTenantRedirect(Context("Assets", "Details", 5)) as RedirectResult;
            Assert.IsNotNull(result);
            StringAssert.Contains("/own-co/Assets/Details/5", result.Url);
        }

        [Test]
        public void LogOff_PassesThrough()
        {
            Assert.IsNull(TenantFilterAttribute.BuildOwnTenantRedirect(Context("Account", "LogOff")));
            Assert.IsTrue(TenantFilterAttribute.IsForeignTenantPassThrough("account", "logoff"));
            Assert.IsFalse(TenantFilterAttribute.IsForeignTenantPassThrough("Account", "Login"));
        }

        [Test]
        public void ChildAction_NeverRedirects()
        {
            Assert.IsNull(TenantFilterAttribute.BuildOwnTenantRedirect(Context("Assets", "Index", child: true)));
        }

        [Test]
        public void NoOwnCompany_NoRedirect()
        {
            Assert.IsNull(TenantFilterAttribute.BuildOwnTenantRedirect(Context("Account", "Login", ownOrg: null)));
            Assert.IsNull(TenantFilterAttribute.BuildOwnTenantRedirect(Context("Account", "Login", ownOrg: 99)), "unknown own org slug");
        }

        [Test]
        public void Filter_RedirectsBeforeAnyOtherBlockOrBranding()
        {
            var source = RepoFile("src", "AssetManagement.Web", "Filters", "TenantFilterAttribute.cs");
            var refused = source.IndexOf("var refusedForeignTenant = ResolveTenantContext(filterContext, tenantToken);", StringComparison.Ordinal);
            var redirect = source.IndexOf("BuildOwnTenantRedirect(filterContext);", StringComparison.Ordinal);
            var email = source.IndexOf("BuildEmailVerificationBlock(filterContext);", StringComparison.Ordinal);
            var license = source.IndexOf("BuildLicenseBlock(filterContext);", StringComparison.Ordinal);
            Assert.That(refused, Is.GreaterThan(0));
            Assert.That(redirect, Is.GreaterThan(refused));
            Assert.That(redirect, Is.LessThan(email));
            Assert.That(redirect, Is.LessThan(license));

            // Branding/scope are only set after the foreign-company guard returns.
            var guard = source.IndexOf("if (IsSignedInToAnotherOrganization(filterContext, organization.Id))", StringComparison.Ordinal);
            var branding = source.IndexOf("ViewBag.OrganizationName = organization.Name;", StringComparison.Ordinal);
            Assert.That(guard, Is.LessThan(branding));
        }

        [Test]
        public void TenantFilter_IsAGlobalFilter()
        {
            StringAssert.Contains("filters.Add(new TenantFilterAttribute());", RepoFile("src", "AssetManagement.Web", "App_Start", "FilterConfig.cs"));
        }

        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AssetManagementModule.sln")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "repository root not found");
            return File.ReadAllText(Path.Combine(new[] { dir.FullName }.Concat(parts).ToArray()));
        }
    }
}