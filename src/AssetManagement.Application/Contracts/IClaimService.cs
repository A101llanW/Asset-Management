using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IClaimService
    {
        void Create(InsuranceClaimVm model);
    }
}
