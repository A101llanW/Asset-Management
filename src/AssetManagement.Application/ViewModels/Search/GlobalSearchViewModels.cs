using System.Collections.Generic;

namespace AssetManagement.Application.ViewModels
{
    public class GlobalSearchResultVm
    {
        public string Query { get; set; }

        public IList<GlobalSearchHitVm> Assets { get; set; }

        public int TotalCount { get; set; }
    }

    public class GlobalSearchHitVm
    {
        public int AssetId { get; set; }

        public string AssetTag { get; set; }

        public string AssetName { get; set; }

        public string SerialNumber { get; set; }

        public string DepartmentName { get; set; }

        public string CustodianName { get; set; }

        public string Status { get; set; }

        public string MatchReason { get; set; }
    }
}
