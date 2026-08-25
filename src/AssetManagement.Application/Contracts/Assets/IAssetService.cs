using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetService
    {
        IEnumerable<AssetListVm> GetAssets(AssetFilterVm filter);

        AssetListPageVm GetAssetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        AssetGroupListPageVm GetAssetGroupListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        AssetGroupMembersPageVm GetAssetGroupMembers(
            AssetFilterVm filter,
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus? groupStatus,
            int skip,
            int take);

        int CountAssets(AssetFilterVm filter);

        AssetDetailsVm GetById(int id);

        AssetScanLookupVm LookupByScanCode(string code, bool applyDepartmentScope = true, bool includeDetails = true);

        AssetTcoVm GetTotalCostOfOwnership(int assetId);

        int Create(AssetCreateVm model);

        void Update(AssetEditVm model);

        void RelocateToClassDepartment(int assetId, int targetDepartmentId, string actorUserId);

        AssetBulkActionResultVm RelocateGroupToClassDepartment(
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus status,
            int targetDepartmentId,
            string actorUserId);

        void UpdateStatus(int id, AssetStatus status);

        void Delete(int id);

        void RequestDisposal(AssetDisposalRequestVm model, string requestedByUserId);

        void ApproveDisposal(AssetDisposalApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin);

        void RejectDisposal(AssetDisposalApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin);
    }
}
