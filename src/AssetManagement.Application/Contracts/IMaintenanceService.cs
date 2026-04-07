using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IMaintenanceService
    {
        void Create(AssetMaintenanceVm model);
    }
}
