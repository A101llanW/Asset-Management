using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Contracts
{
    public interface IAssignmentService
    {
        IEnumerable<AssetAssignmentVm> GetByAsset(int assetId);

        AssignmentListPageVm GetAssignmentListPage(AssignmentFilterVm filter, string sort, string direction, int page, int pageSize);

        void Assign(AssetAssignmentVm model);

        AssetAssignment AssignWithoutSave(AssetAssignmentVm model);
    }
}
