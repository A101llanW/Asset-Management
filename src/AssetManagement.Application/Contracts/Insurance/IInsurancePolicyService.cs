using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IInsurancePolicyService
    {
        IEnumerable<InsurancePolicyListVm> GetByAsset(int assetId);

        InsurancePolicyEditVm GetForEdit(int id);

        int Create(InsurancePolicyEditVm model);

        void Update(InsurancePolicyEditVm model);

        void Delete(int id);
    }
}
