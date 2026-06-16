using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetService
    {
        IEnumerable<AssetListVm> GetAssets(AssetFilterVm filter);

        AssetListPageVm GetAssetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        int CountAssets(AssetFilterVm filter);

        AssetDetailsVm GetById(int id);

        AssetScanLookupVm LookupByScanCode(string code, bool applyDepartmentScope = true, bool includeDetails = true);

        AssetTcoVm GetTotalCostOfOwnership(int assetId);

        int Create(AssetCreateVm model);

        void Update(AssetEditVm model);

        void UpdateStatus(int id, AssetStatus status);

        void Delete(int id);

        void RequestDisposal(AssetDisposalRequestVm model, string requestedByUserId);

        void ApproveDisposal(AssetDisposalApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin);

        void RejectDisposal(AssetDisposalApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin);
    }
}
