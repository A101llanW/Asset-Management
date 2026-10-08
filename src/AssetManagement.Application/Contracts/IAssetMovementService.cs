using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetMovementService
    {
        AssetMovementPageVm GetPage(AssetMovementFilterVm filter);

        byte[] ExportCsv(AssetMovementFilterVm filter);

        /// <summary>Movement history of one asset; the caller has already checked access to the asset.</summary>
        AssetMovementQueryResult GetForAsset(int assetId, int max);
    }
}
