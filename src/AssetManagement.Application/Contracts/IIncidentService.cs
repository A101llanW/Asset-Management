using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IIncidentService
    {
        void Create(AssetIncidentVm model);
    }
}
