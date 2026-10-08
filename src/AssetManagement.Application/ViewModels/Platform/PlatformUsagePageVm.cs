using System;
using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels.Platform
{
    public class PlatformUsagePageVm
    {
        public DateTime GeneratedAtUtc { get; set; }
        public bool ServedFromCache { get; set; }
        public double QueryElapsedMs { get; set; }
        public double DataFileMb { get; set; }
        public double LogFileMb { get; set; }
        public IList<PlatformUsageOrgRowVm> Rows { get; set; }
        public PlatformUsageOrgRowVm Totals { get; set; }
        public string LoginActionCounted { get; set; }
        public string LoginActionsExcludedNote { get; set; }
    }

    public class PlatformUsageOrgRowVm
    {
        public int OrganizationId { get; set; }
        public string Name { get; set; }
        public string Slug { get; set; }
        public bool IsActive { get; set; }
        public int AssetsTotal { get; set; }
        public int AssetsActive { get; set; }
        public int UsersTotal { get; set; }
        public int UsersActive { get; set; }
        public int LoginsLast24h { get; set; }
        public int LoginsLast7d { get; set; }
        public int LoginsLast30d { get; set; }
        public DateTime? LastActivityUtc { get; set; }
        public long StorageRowCount { get; set; }
        public double EstimatedStorageMb { get; set; }
    }
}