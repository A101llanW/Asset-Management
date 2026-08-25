using AssetManagement.Application.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    [TestFixture]
    public class ScanCodeHelperTests
    {
        [Test]
        public void ToLookupKey_IgnoresCaseSpacesAndHyphens()
        {
            Assert.AreEqual("AST2026007", ScanCodeHelper.ToLookupKey("ast-2026-007"));
            Assert.AreEqual("AST2026007", ScanCodeHelper.ToLookupKey("AST 2026 007"));
            Assert.AreEqual("AST2026007", ScanCodeHelper.ToLookupKey("ast2026007"));
        }

        [Test]
        public void ToLookupKey_ExtractsCodeFromQrUrl()
        {
            var key = ScanCodeHelper.ToLookupKey("https://assets.example.com/AssetScan/Lookup?code=AST-2026-007");
            Assert.AreEqual("AST2026007", key);
        }

        [Test]
        public void FieldMatchesLookupKey_UsesFlexibleComparison()
        {
            Assert.IsTrue(ScanCodeHelper.FieldMatchesLookupKey("AST-2026-007", "AST2026007"));
            Assert.IsTrue(ScanCodeHelper.FieldMatchesLookupKey("QR-007", ScanCodeHelper.ToLookupKey("qr 007")));
            Assert.IsFalse(ScanCodeHelper.FieldMatchesLookupKey("AST-2026-008", "AST2026007"));
        }
    }
}
