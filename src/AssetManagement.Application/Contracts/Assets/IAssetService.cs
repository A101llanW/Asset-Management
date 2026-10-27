using System.Collections.Generic;
using AssetManagement.Application.ViewModels;
using AssetManagement.Domain.Enums;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetService
    {
        IEnumerable<AssetListVm> GetAssets(AssetFilterVm filter);

        AssetListPageVm GetAssetListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        AssetPriceSummaryVm GetAssetPriceSummary(AssetFilterVm filter);

        AssetGroupListPageVm GetAssetGroupListPage(AssetFilterVm filter, string sort, string direction, int page, int pageSize);

        AssetGroupMembersPageVm GetAssetGroupMembers(
            AssetFilterVm filter,
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus? groupStatus,
            int? groupCategoryId,
            int? groupAssetTypeId,
            int skip,
            int take);

        int CountAssets(AssetFilterVm filter);

        AssetDetailsVm GetById(int id);

        AssetScanLookupVm LookupByScanCode(string code, bool applyDepartmentScope = true, bool includeDetails = true, bool matchSerialNumber = true);

        AssetTcoVm GetTotalCostOfOwnership(int assetId);

        int Create(AssetCreateVm model);

        /// <summary>
        /// Receiving only (ReceivingService): same as Create, except a blank sub-type is accepted when, and only when, the
        /// asset type has no active sub-types defined. There is no view-model flag for this, so model binding cannot reach it.
        /// </summary>
        int CreateReceived(AssetCreateVm model);

        void Update(AssetEditVm model);

        int CountAcquisitionCostApplyCandidates(int sourceAssetId, string scope, AssetFilterVm filter);

        /// <summary>Sets or clears (null = "Not set") one asset's own price. Permission-checked and audited.</summary>
        void SetAcquisitionCost(int assetId, decimal? acquisitionCost, string actorUserId);

        AssetBulkActionResultVm ApplyAcquisitionCost(
            int sourceAssetId,
            decimal acquisitionCost,
            string scope,
            AssetFilterVm filter,
            string actorUserId);

        void RelocateToClassDepartment(int assetId, int targetDepartmentId, string actorUserId);

        void RelocateToRoomDepartment(int assetId, int targetDepartmentId, string actorUserId);

        AssetBulkActionResultVm RelocateGroupToRoomDepartment(
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus status,
            int targetDepartmentId,
            string actorUserId);

        AssetBulkActionResultVm RelocateGroupToClassDepartment(
            string assetName,
            int? assetSubTypeId,
            int? groupDepartmentId,
            AssetStatus status,
            int targetDepartmentId,
            string actorUserId);

        /// <summary>Flat-list bulk "Move to classroom": moves each selected asset with the single-asset rules; refused ones are skipped.</summary>
        AssetBulkActionResultVm RelocateSelectedToClassDepartment(
            IList<int> assetIds,
            int targetDepartmentId,
            string actorUserId);

        void UpdateStatus(int id, AssetStatus status);

        void Delete(int id);

        void RequestDisposal(AssetDisposalRequestVm model, string requestedByUserId);

        void ApproveDisposal(AssetDisposalApprovalVm model, string approvedByUserId, int? approverRoleId, bool isSuperAdmin);

        void RejectDisposal(AssetDisposalApprovalVm model, string rejectedByUserId, int? approverRoleId, bool isSuperAdmin);
    }
}
