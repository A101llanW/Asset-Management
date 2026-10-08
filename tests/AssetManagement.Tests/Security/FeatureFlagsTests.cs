using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Security
{
    [TestFixture]
    public class FeatureFlagsTests
    {
        [Test]
        public void AssetExcelImportUiEnabled_IsOff_UntilRestored()
        {
            // Code-level switch: flip FeatureFlags.AssetExcelImportUiEnabled to restore Excel import chrome.
            Assert.That(FeatureFlags.AssetExcelImportUiEnabled, Is.False);
        }
    }
}