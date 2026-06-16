using AssetManagement.Infrastructure.Queries;
using AssetManagement.PerformanceTests.Helpers;
using NUnit.Framework;

namespace AssetManagement.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public class SearchPerformanceTests
    {
        private string _connectionString;
        private int _organizationId;

        [TestFixtureSetUp]
        public void FixtureSetUp()
        {
            _connectionString = PerformanceTestSettings.ConnectionString;
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                Assert.Ignore("ASSETMANAGEMENT_TEST_CONNECTION or App.config connection string is required.");
            }

            if (!PerformanceTestSettings.TryGetDefaultOrganizationId(_connectionString, out _organizationId))
            {
                Assert.Ignore("No organization found. Run initialize-database.ps1 first.");
            }

            var assetCount = PerformanceTestSettings.CountActiveAssets(_connectionString, _organizationId);
            if (assetCount < PerformanceTestSettings.LargeDatasetMinimumAssets)
            {
                Assert.Ignore(
                    "Large dataset not seeded (" + assetCount + " assets). Run: .\\initialize-database.ps1 -IncludeLargeDataset");
            }
        }

        [Test]
        public void GlobalSearch_CompletesWithinSla()
        {
            var orgScope = new FixedOrganizationScopeService(_organizationId);
            var departmentScope = new BypassDepartmentScopeService();
            var factory = new StringSqlConnectionFactory(_connectionString);
            var service = new SearchQueryService(factory, orgScope, departmentScope);

            var elapsedMs = PerformanceMeasurement.MeasureElapsedMilliseconds(() =>
            {
                var results = service.GlobalSearch("LDS", 25);
                Assert.IsNotNull(results);
            });

            Assert.LessOrEqual(elapsedMs, PerformanceTestSettings.SearchTimeoutMs,
                "Search p95 target is " + PerformanceTestSettings.SearchTimeoutMs + "ms.");
        }
    }
}
