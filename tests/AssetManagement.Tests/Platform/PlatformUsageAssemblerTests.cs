using System;
using System.Collections.Generic;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.Services.Platform;
using NUnit.Framework;

namespace AssetManagement.Tests.Platform
{
    [TestFixture]
    public class PlatformUsageAssemblerTests
    {
        [Test]
        public void LoginActionCounted_IsHttpAccountLogin_Only()
        {
            Assert.AreEqual("HTTP.Account.Login", PlatformUsageMetrics.LoginActionCounted);
            Assert.That(PlatformUsageMetrics.LoginActionsExcludedNote, Is.StringContaining("VerifyMfa"));
            Assert.That(PlatformUsageMetrics.LoginActionsExcludedNote, Is.StringContaining("ConfirmLegalConsent"));
            Assert.That(PlatformUsageMetrics.LoginActionsExcludedNote, Is.Not.StringContaining("HTTP.Account.Login,"));
        }

        [Test]
        public void Assemble_SumsTotals_AndAllocatesStorageShare()
        {
            var orgs = new List<PlatformUsageOrgMetricDto>
            {
                new PlatformUsageOrgMetricDto { OrganizationId = 1, Name = "Alpha", Slug = "a", IsActive = true },
                new PlatformUsageOrgMetricDto { OrganizationId = 2, Name = "Beta", Slug = "b", IsActive = false }
            };
            var assets = new List<PlatformUsageCountDto>
            {
                new PlatformUsageCountDto { OrganizationId = 1, Total = 10, Active = 8 },
                new PlatformUsageCountDto { OrganizationId = 2, Total = 5, Active = 5 }
            };
            var users = new List<PlatformUsageCountDto>
            {
                new PlatformUsageCountDto { OrganizationId = 1, Total = 3, Active = 2 }
            };
            var logins = new List<PlatformUsageLoginDto>
            {
                new PlatformUsageLoginDto { OrganizationId = 1, Last24h = 1, Last7d = 4, Last30d = 9 }
            };
            var last = new List<PlatformUsageLastActivityDto>
            {
                new PlatformUsageLastActivityDto { OrganizationId = 1, LastActivityUtc = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc) },
                new PlatformUsageLastActivityDto { OrganizationId = 2, LastActivityUtc = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc) }
            };
            var storage = new List<PlatformUsageStorageDto>
            {
                new PlatformUsageStorageDto { OrganizationId = 1, RowCount = 75 },
                new PlatformUsageStorageDto { OrganizationId = 2, RowCount = 25 }
            };
            var db = new PlatformUsageDatabaseSizeDto { DataFileMb = 100d, LogFileMb = 40d };

            var page = PlatformUsageService.Assemble(orgs, assets, users, logins, last, storage, db, DateTime.UtcNow);

            Assert.AreEqual(2, page.Rows.Count);
            Assert.AreEqual("Alpha", page.Rows[0].Name);
            Assert.AreEqual(15, page.Totals.AssetsTotal);
            Assert.AreEqual(13, page.Totals.AssetsActive);
            Assert.AreEqual(3, page.Totals.UsersTotal);
            Assert.AreEqual(9, page.Totals.LoginsLast30d);
            Assert.AreEqual(75d, page.Rows[0].EstimatedStorageMb, 0.001);
            Assert.AreEqual(25d, page.Rows[1].EstimatedStorageMb, 0.001);
            Assert.AreEqual(100d, page.Totals.EstimatedStorageMb, 0.001);
            Assert.AreEqual(new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc), page.Totals.LastActivityUtc);
            Assert.AreEqual(PlatformUsageMetrics.LoginActionCounted, page.LoginActionCounted);
        }
    }
}