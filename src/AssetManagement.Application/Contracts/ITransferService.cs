using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface ITransferService
    {
        void Transfer(AssetTransferVm model);
    }
}
