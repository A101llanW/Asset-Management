using System.Collections.Generic;
using System.Linq;
using AssetManagement.Infrastructure.Identity;
using AssetManagement.Infrastructure.Security;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    /// <summary>
    /// t770u: one email can have a separate account (own Users row, own password hash) in several companies.
    /// Each password reaches only its own company; other companies are never listed or hinted.
    /// </summary>
    [TestFixture]
    public class LoginCandidateRulesTests
    {
        private const int OrgA = 13;
        private const int OrgB = 21;
        private static readonly string HashA = PasswordHasher.HashPassword("Alpha#Pass1");
        private static readonly string HashB = PasswordHasher.HashPassword("Bravo#Pass2");

        private static ApplicationUser User(string id, int? org, string hash, bool active = true)
        {
            return new ApplicationUser { Id = id, Email = "same@person.test", OrganizationId = org, PasswordHash = hash, IsActive = active };
        }

        private static List<ApplicationUser> DifferentPasswords()
        {
            return new List<ApplicationUser> { User("ua", OrgA, HashA), User("ub", OrgB, HashB) };
        }

        private static List<ApplicationUser> SamePassword()
        {
            return new List<ApplicationUser> { User("ua", OrgA, HashA), User("ub", OrgB, HashA) };
        }

        // ---- same password in two companies ----

        [Test]
        public void SamePassword_Root_OffersBothCompanies()
        {
            var r = LoginCandidateRules.Resolve(SamePassword(), "Alpha#Pass1", null);
            Assert.AreEqual(LoginCandidateOutcome.Choose, r.Outcome);
            CollectionAssert.AreEquivalent(new[] { "ua", "ub" }, r.Verified.Select(u => u.Id));
        }

        [TestCase(OrgA, "ua")]
        [TestCase(OrgB, "ub")]
        public void SamePassword_CompanyPortal_SignsInToThatCompanyOnly(int portalOrg, string expected)
        {
            // The repository only returns the portal's own row; pass both to prove the rule ignores the other anyway.
            var r = LoginCandidateRules.Resolve(SamePassword(), "Alpha#Pass1", portalOrg);
            Assert.AreEqual(LoginCandidateOutcome.SignIn, r.Outcome);
            Assert.AreEqual(expected, r.User.Id);
            Assert.AreEqual(1, r.Verified.Count);
        }

        // ---- different password in each company ----

        [TestCase("Alpha#Pass1", "ua")]
        [TestCase("Bravo#Pass2", "ub")]
        public void DifferentPasswords_Root_EachPasswordSignsInToItsOwnCompanyOnly(string password, string expected)
        {
            var r = LoginCandidateRules.Resolve(DifferentPasswords(), password, null);
            Assert.AreEqual(LoginCandidateOutcome.SignIn, r.Outcome);
            Assert.AreEqual(expected, r.User.Id);
            CollectionAssert.AreEqual(new[] { expected }, r.Verified.Select(u => u.Id), "the other company is never listed");
        }

        [Test]
        public void DifferentPasswords_CompanyPortal_OwnPasswordSignsIn()
        {
            var r = LoginCandidateRules.Resolve(DifferentPasswords(), "Bravo#Pass2", OrgB);
            Assert.AreEqual(LoginCandidateOutcome.SignIn, r.Outcome);
            Assert.AreEqual("ub", r.User.Id);
        }

        [Test]
        public void DifferentPasswords_CompanyPortal_OtherCompanysPassword_IsAPlainFailure_WithNothingToList()
        {
            // Company B's password typed on company A's page: no sign-in, and nothing that names company B.
            var r = LoginCandidateRules.Resolve(DifferentPasswords(), "Bravo#Pass2", OrgA);
            Assert.AreEqual(LoginCandidateOutcome.NoMatch, r.Outcome);
            Assert.IsNull(r.User);
            Assert.AreEqual(0, r.Verified.Count);
        }

        // ---- wrong password ----

        [TestCase(null)]
        [TestCase(OrgA)]
        [TestCase(OrgB)]
        public void WrongPassword_IsTheSameNoMatchEverywhere(int? portalOrg)
        {
            foreach (var set in new[] { DifferentPasswords(), SamePassword() })
            {
                var r = LoginCandidateRules.Resolve(set, "Wrong#Pass9", portalOrg);
                Assert.AreEqual(LoginCandidateOutcome.NoMatch, r.Outcome);
                Assert.IsNull(r.User);
                Assert.AreEqual(0, r.Verified.Count);
            }
        }

        [Test]
        public void EmptyPassword_OrNoCandidates_IsNoMatch()
        {
            Assert.AreEqual(LoginCandidateOutcome.NoMatch, LoginCandidateRules.Resolve(DifferentPasswords(), "", null).Outcome);
            Assert.AreEqual(LoginCandidateOutcome.NoMatch, LoginCandidateRules.Resolve(null, "Alpha#Pass1", null).Outcome);
            Assert.AreEqual(LoginCandidateOutcome.NoMatch, LoginCandidateRules.Resolve(new List<ApplicationUser>(), "Alpha#Pass1", OrgA).Outcome);
        }

        // ---- no leaks of other companies ----

        [Test]
        public void Root_ChooserNeverContainsACompanyWhosePasswordDiffers()
        {
            var three = new List<ApplicationUser> { User("ua", OrgA, HashA), User("ub", OrgB, HashB), User("uc", 30, HashA) };
            var r = LoginCandidateRules.Resolve(three, "Alpha#Pass1", null);
            Assert.AreEqual(LoginCandidateOutcome.Choose, r.Outcome);
            CollectionAssert.AreEquivalent(new[] { "ua", "uc" }, r.Verified.Select(u => u.Id));
            Assert.IsFalse(r.Verified.Any(u => u.OrganizationId == OrgB));
        }

        [Test]
        public void InactiveOrHashlessRows_AreNeverMatchedOrListed()
        {
            var rows = new List<ApplicationUser> { User("ua", OrgA, HashA, active: false), User("ub", OrgB, null), User("uc", 30, "not-a-hash") };
            var r = LoginCandidateRules.Resolve(rows, "Alpha#Pass1", null);
            Assert.AreEqual(LoginCandidateOutcome.NoMatch, r.Outcome);
            Assert.AreEqual(0, r.Verified.Count);
        }

        [Test]
        public void PlatformRow_IsNeverUsedOnACompanyPortal()
        {
            var rows = new List<ApplicationUser> { User("platform", null, HashA) };
            Assert.AreEqual(LoginCandidateOutcome.NoMatch, LoginCandidateRules.Resolve(rows, "Alpha#Pass1", OrgA).Outcome);
            Assert.AreEqual("platform", LoginCandidateRules.Resolve(rows, "Alpha#Pass1", null).User.Id);
        }
    }
}