using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Web;
using System.Web.Caching;
using AssetManagement.Application.Contracts.Platform;
using AssetManagement.Application.Contracts.Queries;
using AssetManagement.Application.Helpers;
using AssetManagement.Application.ViewModels.Platform;

namespace AssetManagement.Application.Services.Platform
{
    public class PlatformUsageService : IPlatformUsageService
    {
        public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
        private const string CacheKey = "PlatformUsage.PageVm.v1";

        private readonly IPlatformUsageQueryRepository _queries;

        public PlatformUsageService(IPlatformUsageQueryRepository queries)
        {
            _queries = queries;
        }

        public PlatformUsagePageVm GetUsage(bool bypassCache)
        {
            if (!bypassCache)
            {
                var cached = GetCached();
                if (cached != null)
                {
                    cached.ServedFromCache = true;
                    return cached;
                }
            }

            var sw = Stopwatch.StartNew();
            var page = BuildPage();
            sw.Stop();
            page.QueryElapsedMs = sw.Elapsed.TotalMilliseconds;
            page.ServedFromCache = false;
            SetCached(page);
            return page;
        }

        public static PlatformUsagePageVm Assemble(
            IList<PlatformUsageOrgMetricDto> orgs,
            IList<PlatformUsageCountDto> assets,
            IList<PlatformUsageCountDto> users,
            IList<PlatformUsageLoginDto> logins,
            IList<PlatformUsageLastActivityDto> lastActivity,
            IList<PlatformUsageStorageDto> storage,
            PlatformUsageDatabaseSizeDto dbSize,
            DateTime generatedAtUtc)
        {
            var assetMap = (assets ?? new PlatformUsageCountDto[0]).ToDictionary(x => x.OrganizationId);
            var userMap = (users ?? new PlatformUsageCountDto[0]).ToDictionary(x => x.OrganizationId);
            var loginMap = (logins ?? new PlatformUsageLoginDto[0]).ToDictionary(x => x.OrganizationId);
            var lastMap = (lastActivity ?? new PlatformUsageLastActivityDto[0]).ToDictionary(x => x.OrganizationId);
            var storageMap = (storage ?? new PlatformUsageStorageDto[0]).ToDictionary(x => x.OrganizationId);

            long totalStorageRows = storageMap.Values.Sum(x => x.RowCount);
            double dataMb = dbSize == null ? 0d : dbSize.DataFileMb;

            var rows = new List<PlatformUsageOrgRowVm>();
            foreach (var org in orgs ?? new PlatformUsageOrgMetricDto[0])
            {
                PlatformUsageCountDto assetCount;
                PlatformUsageCountDto userCount;
                PlatformUsageLoginDto loginCount;
                PlatformUsageLastActivityDto last;
                PlatformUsageStorageDto store;
                assetMap.TryGetValue(org.OrganizationId, out assetCount);
                userMap.TryGetValue(org.OrganizationId, out userCount);
                loginMap.TryGetValue(org.OrganizationId, out loginCount);
                lastMap.TryGetValue(org.OrganizationId, out last);
                storageMap.TryGetValue(org.OrganizationId, out store);

                var rowCount = store == null ? 0L : store.RowCount;
                var estimatedMb = totalStorageRows <= 0 || dataMb <= 0
                    ? 0d
                    : dataMb * (rowCount / (double)totalStorageRows);

                rows.Add(new PlatformUsageOrgRowVm
                {
                    OrganizationId = org.OrganizationId,
                    Name = org.Name,
                    Slug = org.Slug,
                    IsActive = org.IsActive,
                    AssetsTotal = assetCount == null ? 0 : assetCount.Total,
                    AssetsActive = assetCount == null ? 0 : assetCount.Active,
                    UsersTotal = userCount == null ? 0 : userCount.Total,
                    UsersActive = userCount == null ? 0 : userCount.Active,
                    LoginsLast24h = loginCount == null ? 0 : loginCount.Last24h,
                    LoginsLast7d = loginCount == null ? 0 : loginCount.Last7d,
                    LoginsLast30d = loginCount == null ? 0 : loginCount.Last30d,
                    LastActivityUtc = last == null ? (DateTime?)null : last.LastActivityUtc,
                    StorageRowCount = rowCount,
                    EstimatedStorageMb = estimatedMb
                });
            }

            rows = rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList();

            var totals = new PlatformUsageOrgRowVm
            {
                OrganizationId = 0,
                Name = "Total",
                Slug = string.Empty,
                IsActive = true,
                AssetsTotal = rows.Sum(r => r.AssetsTotal),
                AssetsActive = rows.Sum(r => r.AssetsActive),
                UsersTotal = rows.Sum(r => r.UsersTotal),
                UsersActive = rows.Sum(r => r.UsersActive),
                LoginsLast24h = rows.Sum(r => r.LoginsLast24h),
                LoginsLast7d = rows.Sum(r => r.LoginsLast7d),
                LoginsLast30d = rows.Sum(r => r.LoginsLast30d),
                LastActivityUtc = rows.Where(r => r.LastActivityUtc.HasValue).Select(r => r.LastActivityUtc.Value).DefaultIfEmpty().Max(),
                StorageRowCount = rows.Sum(r => r.StorageRowCount),
                EstimatedStorageMb = rows.Sum(r => r.EstimatedStorageMb)
            };

            if (totals.LastActivityUtc == default(DateTime))
            {
                totals.LastActivityUtc = null;
            }

            return new PlatformUsagePageVm
            {
                GeneratedAtUtc = generatedAtUtc,
                DataFileMb = dataMb,
                LogFileMb = dbSize == null ? 0d : dbSize.LogFileMb,
                Rows = rows,
                Totals = totals,
                LoginActionCounted = PlatformUsageMetrics.LoginActionCounted,
                LoginActionsExcludedNote = PlatformUsageMetrics.LoginActionsExcludedNote
            };
        }

        private PlatformUsagePageVm BuildPage()
        {
            var since30d = DateTime.UtcNow.AddDays(-30);
            var orgs = _queries.GetOrganizations();
            var assets = _queries.GetAssetCounts();
            var users = _queries.GetUserCounts();
            var logins = _queries.GetLoginCountsSince(since30d);
            var lastActivity = _queries.GetLastActivityByOrganization();
            var storage = _queries.GetStorageRowCounts();
            var dbSize = _queries.GetDatabaseSize();
            return Assemble(orgs, assets, users, logins, lastActivity, storage, dbSize, DateTime.UtcNow);
        }

        private static PlatformUsagePageVm GetCached()
        {
            if (HttpRuntime.Cache == null)
            {
                return null;
            }

            return HttpRuntime.Cache[CacheKey] as PlatformUsagePageVm;
        }

        private static void SetCached(PlatformUsagePageVm page)
        {
            if (HttpRuntime.Cache == null || page == null)
            {
                return;
            }

            HttpRuntime.Cache.Insert(
                CacheKey,
                page,
                null,
                DateTime.UtcNow.Add(CacheTtl),
                Cache.NoSlidingExpiration,
                CacheItemPriority.Default,
                null);
        }
    }
}