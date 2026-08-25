using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class DemoLoginEmailHelperTests
    {
        [Test]
        public void ResolveLoginEmail_MapsLegacyDefaultTenantAdminToNanosoft()
        {
            Assert.AreEqual(
                "nanosoft@asset.local",
                DemoLoginEmailHelper.ResolveLoginEmail("default@asset.local", null));
        }

        [Test]
        public void ResolveLoginEmail_PreservesCanonicalTenantAdminEmail()
        {
            Assert.AreEqual(
                "nanosoft@asset.local",
                DemoLoginEmailHelper.ResolveLoginEmail("nanosoft@asset.local", "nanosoft"));
        }

        [Test]
        public void ResolveLoginEmail_PreservesPlatformAdminEmail()
        {
            Assert.AreEqual(
                DemoLoginEmailHelper.PlatformAdminEmail,
                DemoLoginEmailHelper.ResolveLoginEmail(DemoLoginEmailHelper.PlatformAdminEmail, null));
        }
    }
}
