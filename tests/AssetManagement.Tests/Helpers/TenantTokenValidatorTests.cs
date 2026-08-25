using System;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class TenantTokenValidatorTests
    {
        private static readonly Func<string, bool> ReservedControllers = segment =>
            string.Equals(segment, "Assets", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "Dashboard", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "Account", StringComparison.OrdinalIgnoreCase);

        [Test]
        public void IsPlausibleToken_RejectsReservedControllerNames()
        {
            Assert.IsFalse(TenantTokenValidator.IsPlausibleToken("Assets", ReservedControllers));
            Assert.IsFalse(TenantTokenValidator.IsPlausibleToken("Dashboard", ReservedControllers));
            Assert.IsFalse(TenantTokenValidator.IsPlausibleToken("Account", ReservedControllers));
        }

        [Test]
        public void IsPlausibleToken_AcceptsRecruitmentStyleSlugs()
        {
            Assert.IsTrue(TenantTokenValidator.IsPlausibleToken("N66109465", ReservedControllers));
        }

        [Test]
        public void IsPlausibleToken_AcceptsWordSlugs()
        {
            Assert.IsTrue(TenantTokenValidator.IsPlausibleToken("nanosoft", ReservedControllers));
        }
    }
}
