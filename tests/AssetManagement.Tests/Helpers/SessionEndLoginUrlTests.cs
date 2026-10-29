using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>
    /// QA 2026-10-29: an ended session (revoked token after Log off, fingerprint mismatch, lockout) goes back to the
    /// TENANT login page, never to another company's page and never to an unvalidated URL (no open redirect).
    /// </summary>
    [TestFixture]
    public class SessionEndLoginUrlTests
    {
        private static readonly Dictionary<string, string> KnownSlugs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "a46138179", "a46138179" },
            { "nis", "nis" }
        };

        private static string Known(string segment)
        {
            string slug;
            return KnownSlugs.TryGetValue(segment, out slug) ? slug : null;
        }

        private static string Build(string path, string rawUrl, string method, string reason, string ownSlug)
        {
            return SessionEndLoginUrl.Build(path, rawUrl, method, reason, Known, ownSlug, "/Account/Login");
        }

        [Test]
        public void ReplayedCookieOnTenantDashboard_KeepsTheTenant()
        {
            Assert.AreEqual(
                "/a46138179/Account/Login?reason=session_invalid",
                Build("~/a46138179/Dashboard", "/a46138179/Dashboard", "GET", "session_invalid", "a46138179"));
        }

        [Test]
        public void TenantPage_CarriesALocalReturnUrl()
        {
            Assert.AreEqual(
                "/a46138179/Account/Login?reason=session_invalid&ReturnUrl=%2fa46138179%2fAssets%3fx%3d1",
                Build("~/a46138179/Assets", "/a46138179/Assets?x=1", "GET", "session_invalid", "a46138179"));
        }

        [Test]
        public void UnknownTicketOrganisation_UsesAKnownRequestTenant()
        {
            StringAssert.StartsWith(
                "/a46138179/Account/Login?reason=session_invalid",
                Build("~/a46138179/Assets", "/a46138179/Assets", "GET", "session_invalid", null));
        }

        [Test]
        public void OwnOrganisationWins_OverAnotherCompanysUrl_AndDropsTheForeignReturnUrl()
        {
            Assert.AreEqual(
                "/a46138179/Account/Login?reason=session_invalid",
                Build("~/nis/Assets/Details/5", "/nis/Assets/Details/5", "GET", "session_invalid", "a46138179"));
        }

        [TestCase("~/evilcorp/Assets", "/evilcorp/Assets")]
        [TestCase("~/Account/Profile", "/Account/Profile")]
        [TestCase("~/Content/site.css", "/Content/site.css")]
        [TestCase("~/", "/")]
        [TestCase(null, null)]
        public void UnknownOrReservedSegment_GoesToTheRootLogin(string path, string rawUrl)
        {
            Assert.AreEqual("/Account/Login?reason=session_invalid", Build(path, rawUrl, "GET", "session_invalid", null));
        }

        [Test]
        public void ResolverFailure_GoesToTheRootLogin()
        {
            var url = SessionEndLoginUrl.Build("~/nis/Assets", "/nis/Assets", "GET", "session_invalid",
                s => { throw new InvalidOperationException("db down"); }, null, "/Account/Login");
            Assert.AreEqual("/Account/Login?reason=session_invalid", url);
        }

        [Test]
        public void Post_HasNoReturnUrl()
        {
            Assert.AreEqual(
                "/a46138179/Account/Login?reason=session_invalid",
                Build("~/a46138179/Assets/Edit/5", "/a46138179/Assets/Edit/5", "POST", "session_invalid", "a46138179"));
        }

        [TestCase("//evil.example/x")]
        [TestCase("/\\evil.example/x")]
        [TestCase("http://evil.example/x")]
        [TestCase("/a46138179/Assets?next=https://evil.example")]
        [TestCase("/a46138179/Account/Profile")]
        public void UnsafeOrAccountReturnUrl_IsDropped(string rawUrl)
        {
            Assert.AreEqual(
                "/a46138179/Account/Login?reason=session_invalid",
                Build("~/a46138179/Assets", rawUrl, "GET", "session_invalid", "a46138179"));
        }

        [TestCase("session_hijack", "session_hijack")]
        [TestCase("account_locked", "account_locked")]
        [TestCase("<script>", "session_invalid")]
        [TestCase(null, "session_invalid")]
        public void Reason_IsAllowListed(string reason, string expected)
        {
            Assert.AreEqual(
                "/a46138179/Account/Login?reason=" + expected,
                Build("~/a46138179/Dashboard", "/a46138179/Dashboard", "GET", reason, "a46138179"));
        }

        [Test]
        public void InvalidOwnSlug_IsIgnored()
        {
            Assert.AreEqual("/Account/Login?reason=session_invalid", Build("~/evilcorp/x", "/evilcorp/x", "GET", null, "Account"));
        }

        // ---- wiring (source checks) ----

        [Test]
        public void GlobalAsax_InvalidateSession_UsesTheTenantLoginUrl()
        {
            var source = RepoFile("src", "AssetManagement.Web", "Global.asax.cs");
            StringAssert.Contains("SessionEndLoginUrl.Build(", source);
            Assert.IsFalse(Regex.IsMatch(source, @"InvalidateSession\(""[a-z_]+""\);"), "every InvalidateSession call passes the ticket organisation");
        }

        [Test]
        public void AccountAndDocuments_UseTenantAuthorize_SoAnonymousRedirectsKeepTheTenant()
        {
            var account = RepoFile("src", "AssetManagement.Web", "Controllers", "AccountController.cs");
            var documents = RepoFile("src", "AssetManagement.Web", "Controllers", "DocumentsController.cs");
            Assert.IsFalse(Regex.IsMatch(account, @"^\s*\[Authorize\]\s*$", RegexOptions.Multiline));
            Assert.IsFalse(Regex.IsMatch(documents, @"^\s*\[Authorize\]\s*$", RegexOptions.Multiline));
        }

        [Test]
        public void TenantFilter_DoesNotAdoptAnotherCompanysScope()
        {
            var source = RepoFile("src", "AssetManagement.Web", "Filters", "TenantFilterAttribute.cs");
            var guard = source.IndexOf("if (IsSignedInToAnotherOrganization(filterContext, organization.Id))", StringComparison.Ordinal);
            var scope = source.IndexOf("organizationScope.SetExecutionContext(", StringComparison.Ordinal);
            Assert.That(guard, Is.GreaterThan(0));
            Assert.That(guard, Is.LessThan(scope), "the guard runs before the URL organisation becomes the scope");
        }

        [Test]
        public void LoginPortalHints_RequireTheVerifiedPassword()
        {
            var source = RepoFile("src", "AssetManagement.Web", "Controllers", "AccountController.cs");
            // t770u: the only chooser is built from the accounts whose own password matched, and a company portal never
            // looks up the email in other companies.
            Assert.AreEqual(1, Regex.Matches(source, @"BuildPortalCandidates\(").Count - 1, "one chooser call site");
            StringAssert.Contains("BuildPortalCandidates(resolution.Verified,", source);
            StringAssert.Contains("LoginCandidateRules.Resolve(candidates, model.Password, organizationId)", source);
            StringAssert.DoesNotContain("FindActiveByEmail(model.Email, null)", source);
            StringAssert.DoesNotContain("Select your organization below", source);
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