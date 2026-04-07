using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssignmentService
    {
        IEnumerable<AssetAssignmentVm> GetByAsset(int assetId);

        void Assign(AssetAssignmentVm model);
    }
}
