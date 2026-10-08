using System;
using System.Collections.Generic;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IPlatformUsageQueryRepository
    {
        IList<PlatformUsageOrgMetricDto> GetOrganizations();
        IList<PlatformUsageCountDto> GetAssetCounts();
        IList<PlatformUsageCountDto> GetUserCounts();
        IList<PlatformUsageLoginDto> GetLoginCountsSince(DateTime sinceUtc);
        IList<PlatformUsageLastActivityDto> GetLastActivityByOrganization();
        IList<PlatformUsageStorageDto> GetStorageRowCounts();
        PlatformUsageDatabaseSizeDto GetDatabaseSize();
    }

    public class PlatformUsageOrgMetricDto
    {
        public int OrganizationId { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public bool IsActive { get; set; }
    }

    public class PlatformUsageCountDto
    {
        public int OrganizationId { get; set; }
        public int Total { get; set; }
        public int Active { get; set; }
    }

    public class PlatformUsageLoginDto
    {
        public int OrganizationId { get; set; }
        public int Last24h { get; set; }
        public int Last7d { get; set; }
        public int Last30d { get; set; }
    }

    public class PlatformUsageLastActivityDto
    {
        public int OrganizationId { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }

    public class PlatformUsageStorageDto
    {
        public int OrganizationId { get; set; }
        public long RowCount { get; set; }
    }

    public class PlatformUsageDatabaseSizeDto
    {
        public double DataFileMb { get; set; }
        public double LogFileMb { get; set; }
    }
}