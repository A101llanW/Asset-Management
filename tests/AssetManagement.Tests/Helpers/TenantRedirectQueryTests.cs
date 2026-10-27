using AssetManagement.Web.Helpers;
using NUnit.Framework;

namespace AssetManagement.Tests.Helpers
{
    /// <summary>QA S1.8 + tenantless QR links (2026-10-27): redirects keep the query string.</summary>
    [TestFixture]
    public class TenantRedirectQueryTests
    {
        [TestCase("/a46138179/Assets", "?Section=Prep", "/a46138179/Assets?Section=Prep")]
        [TestCase("/a46138179/AssetScan/Lookup", "?code=ACA-G2A-001", "/a46138179/AssetScan/Lookup?code=ACA-G2A-001")]
        [TestCase("/a46138179/Assets", "Section=Prep&page=2", "/a46138179/Assets?Section=Prep&page=2")]
        [TestCase("/a46138179/Assets?x=1", "?Section=Prep", "/a46138179/Assets?x=1&Section=Prep")]
        [TestCase("/a46138179/Assets", "", "/a46138179/Assets")]
        [TestCase("/a46138179/Assets", "?", "/a46138179/Assets")]
        [TestCase("/a46138179/Assets", null, "/a46138179/Assets")]
        public void AppendQueryString_KeepsTheQuery(string url, string query, string expected)
        {
            Assert.AreEqual(expected, TenantUrlHelper.AppendQueryString(url, query));
        }

        [Test]
        public void QueryToCarry_NullRequest_IsNull()
        {
            Assert.IsNull(TenantUrlHelper.QueryToCarry(null));
        }
    }
}
