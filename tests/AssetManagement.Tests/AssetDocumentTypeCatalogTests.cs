using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests
{
    [TestFixture]
    public class AssetDocumentTypeCatalogTests
    {
        [Test]
        public void GetAllSuggestedTypes_IncludesStandardAndAdditionalTypes()
        {
            var types = AssetDocumentTypeCatalog.GetAllSuggestedTypes(new[] { "Board approval letter" });

            Assert.IsTrue(types.Contains("Purchase invoice"));
            Assert.IsTrue(types.Contains("Board approval letter"));
        }

        [Test]
        public void NormalizeType_TrimsAndCapsLength()
        {
            var normalized = AssetDocumentTypeCatalog.NormalizeType("  Warranty certificate  ");

            Assert.AreEqual("Warranty certificate", normalized);
            Assert.IsNull(AssetDocumentTypeCatalog.NormalizeType("   "));
        }

        [Test]
        public void IsPhotoMediaType_RecognizesPhotosAndMediaCatalogTypes()
        {
            Assert.IsTrue(AssetDocumentTypeCatalog.IsPhotoMediaType("Damage photo"));
            Assert.IsFalse(AssetDocumentTypeCatalog.IsPhotoMediaType("Purchase invoice"));
        }

        [Test]
        public void FormatPhotoDocumentType_UsesPhotoNameWhenProvided()
        {
            var formatted = AssetDocumentTypeCatalog.FormatPhotoDocumentType("Damage photo", "condition before photo");

            Assert.AreEqual("condition before photo", formatted);
        }
    }
}
