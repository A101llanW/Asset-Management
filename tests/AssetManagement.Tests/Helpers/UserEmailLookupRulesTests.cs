using System.Collections.Generic;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class UserEmailLookupRulesTests
    {
        [Test]
        public void ResolveUnscoped_PrefersPlatformOverTenant()
        {
            var candidates = new List<UserEmailCandidate>
            {
                new UserEmailCandidate { Id = "t1", OrganizationId = 10, IsActive = true },
                new UserEmailCandidate { Id = "p1", OrganizationId = null, IsActive = true }
            };

            Assert.AreEqual("p1", UserEmailLookupRules.ResolveUnscopedUserId(candidates, true));
        }

        [Test]
        public void ResolveUnscoped_ReturnsSoleTenantMatch()
        {
            var candidates = new List<UserEmailCandidate>
            {
                new UserEmailCandidate { Id = "t1", OrganizationId = 10, IsActive = true }
            };

            Assert.AreEqual("t1", UserEmailLookupRules.ResolveUnscopedUserId(candidates, true));
        }

        [Test]
        public void ResolveUnscoped_ReturnsNull_WhenEmailExistsInMultipleOrgs()
        {
            var candidates = new List<UserEmailCandidate>
            {
                new UserEmailCandidate { Id = "t1", OrganizationId = 10, IsActive = true },
                new UserEmailCandidate { Id = "t2", OrganizationId = 11, IsActive = true }
            };

            Assert.IsNull(UserEmailLookupRules.ResolveUnscopedUserId(candidates, true));
            Assert.IsTrue(UserEmailLookupRules.IsAmbiguousAcrossTenants(candidates, true));
        }

        [Test]
        public void ResolveUnscoped_IgnoresInactive_WhenActiveOnly()
        {
            var candidates = new List<UserEmailCandidate>
            {
                new UserEmailCandidate { Id = "dead", OrganizationId = 10, IsActive = false },
                new UserEmailCandidate { Id = "live", OrganizationId = 11, IsActive = true }
            };

            Assert.AreEqual("live", UserEmailLookupRules.ResolveUnscopedUserId(candidates, true));
        }

        [Test]
        public void ResolveUnscoped_ReturnsNull_WhenMultiplePlatformRows()
        {
            var candidates = new List<UserEmailCandidate>
            {
                new UserEmailCandidate { Id = "p1", OrganizationId = null, IsActive = true },
                new UserEmailCandidate { Id = "p2", OrganizationId = null, IsActive = true }
            };

            Assert.IsNull(UserEmailLookupRules.ResolveUnscopedUserId(candidates, true));
        }

        [Test]
        public void ResolveUnscoped_Empty_ReturnsNull()
        {
            Assert.IsNull(UserEmailLookupRules.ResolveUnscopedUserId(null, true));
            Assert.IsNull(UserEmailLookupRules.ResolveUnscopedUserId(new UserEmailCandidate[0], true));
        }
    }
}
