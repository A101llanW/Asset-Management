using System;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class AssetScanUrlHelperTests
    {
        [Test]
        public void ResolveBaseUrl_PrefersConfiguredExternalBaseUrl()
        {
            var result = AssetScanUrlHelper.ResolveBaseUrl("https://assets.example.com/", "http://localhost");

            Assert.AreEqual("https://assets.example.com", result);
        }

        [Test]
        public void ResolveBaseUrl_FallsBackToRequestAuthority()
        {
            var result = AssetScanUrlHelper.ResolveBaseUrl(null, "http://localhost:8080/");

            Assert.AreEqual("http://localhost:8080", result);
        }

        [Test]
        public void ResolvePasswordResetBaseUrl_PrefersRequestPortOnLocalhostDevMismatch()
        {
            var result = AssetScanUrlHelper.ResolvePasswordResetBaseUrl(
                "http://localhost",
                new Uri("http://localhost:51901/nanosoft/Account/ForgotPassword"));

            Assert.AreEqual("http://localhost:51901", result);
        }

        [Test]
        public void ResolvePasswordResetBaseUrl_UsesConfiguredProductionUrl()
        {
            var result = AssetScanUrlHelper.ResolvePasswordResetBaseUrl(
                "https://assets.example.com",
                new Uri("http://localhost:51901/Account/ForgotPassword"));

            Assert.AreEqual("https://assets.example.com", result);
        }

        [Test]
        public void CombineBaseAndRelative_BuildsAbsoluteScanUrl()
        {
            var result = AssetScanUrlHelper.CombineBaseAndRelative(
                "https://assets.example.com",
                "/tenant/AssetScan/Lookup?code=AST-001");

            Assert.AreEqual("https://assets.example.com/tenant/AssetScan/Lookup?code=AST-001", result);
        }
    }
}
