using AssetManagement.Application;
using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Assets
{
    [TestFixture]
    public class DisplayTextTests
    {
        [Test]
        public void FormatBrandModel_HidesLegacyImportPlaceholders()
        {
            Assert.AreEqual(string.Empty, DisplayText.FormatBrandModel(LegacyImportDefaults.Brand, LegacyImportDefaults.Model));
            Assert.AreEqual(string.Empty, DisplayText.FormatBrandModel("unknown", "legacy import"));
            Assert.AreEqual(string.Empty, DisplayText.FormatBrandModel(" Unknown ", " Legacy Import "));
        }

        [Test]
        public void FormatBrandModel_ShowsRealBrandAndModel()
        {
            Assert.AreEqual("Dell Latitude 5520", DisplayText.FormatBrandModel("Dell", "Latitude 5520"));
            Assert.AreEqual("HP", DisplayText.FormatBrandModel("HP", LegacyImportDefaults.Model));
            Assert.AreEqual("Latitude 5520", DisplayText.FormatBrandModel(LegacyImportDefaults.Brand, "Latitude 5520"));
        }

        [Test]
        public void FormatBrandModel_OmitsBlankParts()
        {
            Assert.AreEqual("Dell", DisplayText.FormatBrandModel("Dell", null));
            Assert.AreEqual("Latitude", DisplayText.FormatBrandModel(string.Empty, "Latitude"));
            Assert.AreEqual(string.Empty, DisplayText.FormatBrandModel(null, "   "));
        }
    }
}
