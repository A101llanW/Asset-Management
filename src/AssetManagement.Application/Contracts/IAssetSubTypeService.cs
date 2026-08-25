using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
namespace AssetManagement.Application.Contracts
{
    public interface IAssetSubTypeService
    {
        AssetSubTypeVm GetById(int id);
        IEnumerable<AssetSubTypeListItemVm> GetByAssetTypeId(int assetTypeId, bool activeOnly = true);
        AssetSubTypeVm Lookup(int assetTypeId, string brand, string model);
        int Create(AssetSubTypeEditVm model);
        void Update(AssetSubTypeEditVm model);
        int CreateFromAsset(AssetSubTypeCreateFromAssetVm model);
        int CountAssets(int subTypeId);
        int GetTotalQuantityOnHand(int subTypeId);
    }
}
