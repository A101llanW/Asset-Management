using System;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IAssetQueryService
    {
        AssetListPageVm GetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        AssetGroupListPageVm GetGroupedListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        AssetGroupMembersPageVm GetGroupMembers(
            AssetFilterVm filter,
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus groupStatus,
            int skip,
            int take);

        int Count(AssetFilterVm filter);

        AssetExportResultVm StreamExport(AssetFilterVm filter, string sort, string direction, Action<AssetExportRowVm> writeRow, int? maxRows = null);
    }
}
