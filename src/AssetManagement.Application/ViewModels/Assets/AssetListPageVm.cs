using System.Collections.Generic;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.ViewModels
{
    /// <summary>Priced total and counts; unpriced ("Not set") assets are counted, never summed.</summary>
    public class AssetPriceSummaryVm
    {
        public int TotalCount { get; set; }

        public int PricedCount { get; set; }

        public int UnpricedCount { get; set; }

        public decimal PricedTotal { get; set; }
    }

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

        public string GroupLabel { get; set; }

        public string AssetName { get; set; }

        public int? CategoryId { get; set; }

        public int? AssetTypeId { get; set; }

        public string CategoryName { get; set; }

        public string DepartmentName { get; set; }

        public int? DepartmentId { get; set; }

        public int? AssetSubTypeId { get; set; }

        public string AssetSubTypeName { get; set; }

        public AssetStatus CurrentStatus { get; set; }

        public int Count { get; set; }

        /// <summary>Sum of priced members only; unpriced ("Not set") members are excluded.</summary>
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

        public string GroupBy { get; set; }
    }

    public class AssetGroupMembersPageVm
    {
        public IList<AssetListVm> Items { get; set; } = new List<AssetListVm>();

        public int TotalCount { get; set; }

        public int Skip { get; set; }

        public int Take { get; set; }
    }
}
