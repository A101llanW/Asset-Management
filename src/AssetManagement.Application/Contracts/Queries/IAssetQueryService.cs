using System;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IAssetQueryService
    {
        AssetListPageVm GetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        int Count(AssetFilterVm filter);

        AssetExportResultVm StreamExport(AssetFilterVm filter, string sort, string direction, Action<AssetExportRowVm> writeRow, int? maxRows = null);
    }
}
