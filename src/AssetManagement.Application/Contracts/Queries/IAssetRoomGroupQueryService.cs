using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    /// <summary>
    /// Assets Index "Group by room": aggregate-first, grant-free reads over the same tenant /
    /// department-scoped, active-only, filtered asset set as the Assets Index list.
    /// </summary>
    public interface IAssetRoomGroupQueryService
    {
        /// <summary>Level 1: one row per unit (rooms first, by name), paged in memory over the per-unit aggregate.</summary>
        AssetRoomGroupPageVm GetRoomPage(AssetFilterVm filter, int page, int pageSize);

        /// <summary>Level 2: categories of the filtered assets in one unit (roomId 0 = no unit), by name.</summary>
        IList<AssetRoomCategoryGroupVm> GetRoomCategories(AssetFilterVm filter, int roomId);

        /// <summary>Level 3: the filtered assets in one unit + category, by tag, paged on ids first.</summary>
        AssetRoomAssetPageVm GetRoomCategoryAssets(AssetFilterVm filter, int roomId, int categoryId, int skip, int take);
    }
}
