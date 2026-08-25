using System.Linq;
using AssetManagement.Application.ViewModels;
using AssetManagement.Infrastructure.Queries;
using AssetManagement.PerformanceTests.Helpers;
using NUnit.Framework;

namespace AssetManagement.PerformanceTests
{
    [TestFixture]
    [Category("Performance")]
    public class AssetGroupLazyLoadTests
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

            PerformanceTestSettings.EnsureGroupedAssetSchema(_connectionString);

            if (!PerformanceTestSettings.TryGetDefaultOrganizationId(_connectionString, out _organizationId))
            {
                Assert.Ignore("No organization found. Run tools/database/Initialize-Database.ps1 first.");
            }

            if (!PerformanceTestSettings.TableExists(_connectionString, "AssetSubType"))
            {
                Assert.Ignore("AssetSubType table is missing after migration apply. Run tools/database/Initialize-Database.ps1 or tools/database/Invoke-Migrations.ps1.");
            }
        }

        [Test]
        public void GetGroupedListPage_DoesNotEagerLoadMembers()
        {
            var orgScope = new FixedOrganizationScopeService(_organizationId);
            var departmentScope = new BypassDepartmentScopeService();
            var factory = new StringSqlConnectionFactory(_connectionString);
            var service = new AssetQueryService(factory, orgScope, departmentScope);

            var page = service.GetGroupedListPage(new AssetFilterVm(), "count", "desc", 1, 10);
            Assert.IsNotNull(page);
            Assert.IsNotNull(page.Items);

            if (page.Items.Count == 0)
            {
                Assert.Ignore("No asset groups available for lazy-load verification.");
            }

            foreach (var group in page.Items)
            {
                Assert.IsNotNull(group.Members);
                Assert.AreEqual(0, group.Members.Count, "Grouped list page should not load members eagerly.");
            }
        }

        [Test]
        public void GetGroupMembers_ReturnsPagedMembersForFirstGroup()
        {
            var orgScope = new FixedOrganizationScopeService(_organizationId);
            var departmentScope = new BypassDepartmentScopeService();
            var factory = new StringSqlConnectionFactory(_connectionString);
            var service = new AssetQueryService(factory, orgScope, departmentScope);
            var filter = new AssetFilterVm();

            var page = service.GetGroupedListPage(filter, "count", "desc", 1, 1);
            if (page.Items.Count == 0)
            {
                Assert.Ignore("No asset groups available for member paging verification.");
            }

            var group = page.Items.First();
            var members = service.GetGroupMembers(
                filter,
                group.AssetName,
                group.AssetSubTypeId,
                group.DepartmentId,
                group.CurrentStatus,
                skip: 0,
                take: 10);

            Assert.IsNotNull(members);
            Assert.AreEqual(group.Count, members.TotalCount);
            Assert.LessOrEqual(members.Items.Count, 10);
            Assert.Greater(members.Items.Count, 0);
        }
    }
}
