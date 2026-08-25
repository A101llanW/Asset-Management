namespace AssetManagement.Application.Contracts
{
    public interface IAssetStockService
    {
        int GetAvailableQuantity(int subTypeId, int? departmentId);
        int GetAvailableQuantityForAsset(int assetId, int? departmentId);
    }
}
