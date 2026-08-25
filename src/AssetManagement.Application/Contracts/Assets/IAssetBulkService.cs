using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetBulkService
    {
        AssetBulkActionResultVm Execute(AssetBulkActionRequestVm request, string actorUserId);
    }
}
