using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    public class AssetListPageVm
    {
        public IList<AssetListVm> Items { get; set; } = new List<AssetListVm>();

        public int TotalCount { get; set; }

        public string Search { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class AssetGroupListVm
    {
        public string GroupKey { get; set; }

        public string AssetName { get; set; }

        public string CategoryName { get; set; }

        public string DepartmentName { get; set; }

        public int? DepartmentId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public string AssetSubTypeName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public int Count { get; set; }

        public decimal TotalAcquisitionCost { get; set; }

        public IList<AssetListVm> Members { get; set; } = new List<AssetListVm>();
    }

    public class AssetGroupListPageVm
    {
        public IList<AssetGroupListVm> Items { get; set; } = new List<AssetGroupListVm>();

        public int TotalCount { get; set; }

        public string Search { get; set; }

        public string Sort { get; set; }

        public string Direction { get; set; }

        public int Page { get; set; }

        public int PageSize { get; set; }
    }

    public class AssetGroupMembersPageVm
    {
        public IList<AssetListVm> Items { get; set; } = new List<AssetListVm>();

        public int TotalCount { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; }
    }
}
