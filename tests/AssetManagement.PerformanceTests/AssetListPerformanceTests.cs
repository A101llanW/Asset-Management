using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.PerformanceTests.Helpers;
using NUnit.Framework;

namespace AssetManagement.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public class AssetListPerformanceTests
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
        public void AssetListPage_CompletesWithinSla()
        {
            var orgScope = new FixedOrganizationScopeService(_organizationId);
            var departmentScope = new BypassDepartmentScopeService();
            var factory = new StringSqlConnectionFactory(_connectionString);
            var service = new AssetQueryService(factory, orgScope, departmentScope);

            var elapsedMs = PerformanceMeasurement.MeasureElapsedMilliseconds(() =>
            {
                var page = service.GetListPage(new AssetFilterVm(), "tag", "asc", 1, 25);
                Assert.IsNotNull(page);
                Assert.IsNotNull(page.Items);
                Assert.Greater(page.TotalCount, 0);
                Assert.LessOrEqual(page.Items.Count, 25);
            });

            Assert.LessOrEqual(elapsedMs, PerformanceTestSettings.AssetListTimeoutMs,
                "Asset list p95 target is " + PerformanceTestSettings.AssetListTimeoutMs + "ms.");
        }

        [Test]
        public void AssetListPage_WorkingSetDelta_StaysUnderBudget()
        {
            var orgScope = new FixedOrganizationScopeService(_organizationId);
            var departmentScope = new BypassDepartmentScopeService();
            var factory = new StringSqlConnectionFactory(_connectionString);
            var service = new AssetQueryService(factory, orgScope, departmentScope);

            var delta = PerformanceMeasurement.MeasureWorkingSetDelta(() =>
            {
                service.GetListPage(new AssetFilterVm(), "tag", "asc", 1, 25);
            });

            Assert.LessOrEqual(delta, PerformanceTestSettings.MaxListWorkingSetBytes,
                "List page working set delta should stay under 5 MB.");
        }
    }
}
