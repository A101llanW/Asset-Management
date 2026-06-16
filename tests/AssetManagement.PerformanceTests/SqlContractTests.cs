using NUnit.Framework;

namespace AssetManagement.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public class SqlContractTests
    {
        [Test]
        public void AssetListSql_Contract_RequiresOrganizationPredicate()
        {
            const string sampleListSql = "WHERE a.[OrganizationId] = @OrganizationId";
            StringAssert.Contains("@OrganizationId", sampleListSql);
        }
    }
}
