using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IReturnService
    {
        void ReturnAsset(AssetReturnVm model);
    }
}
