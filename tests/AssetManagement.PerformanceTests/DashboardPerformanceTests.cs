using AssetManagement.Infrastructure.Queries;
using AssetManagement.PerformanceTests.Helpers;
using NUnit.Framework;

namespace AssetManagement.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public class DashboardPerformanceTests
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
        public void DashboardKpis_CompletesWithinSla()
        {
            var factory = new StringSqlConnectionFactory(_connectionString);
            var service = new DashboardQueryService(factory);

            var elapsedMs = PerformanceMeasurement.MeasureElapsedMilliseconds(() =>
            {
                var kpis = service.GetKpis(_organizationId, null, true, false);
                Assert.IsNotNull(kpis);
            });

            Assert.LessOrEqual(elapsedMs, PerformanceTestSettings.DashboardTimeoutMs,
                "Dashboard p95 target is " + PerformanceTestSettings.DashboardTimeoutMs + "ms.");
        }
    }
}
