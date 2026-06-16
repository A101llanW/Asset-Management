using System.Collections.Generic;

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
}
