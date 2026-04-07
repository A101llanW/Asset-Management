using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetService
    {
        IEnumerable<AssetListVm> GetAssets(AssetFilterVm filter);

        AssetDetailsVm GetById(int id);

        int Create(AssetCreateVm model);

        void Update(AssetEditVm model);

        void Delete(int id);

        void RequestDisposal(AssetDisposalRequestVm model, string requestedByUserId);

        void ApproveDisposal(AssetDisposalApprovalVm model, string approvedByUserId);
    }
}
