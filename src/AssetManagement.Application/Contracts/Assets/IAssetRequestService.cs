using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts
{
    public interface IAssetRequestService
    {
        AssetRequestListPageVm GetRequests(AssetRequestFilterVm filter, string sort, string direction, int page, int pageSize);

        AssetRequestDetailsVm GetById(int id);

        int Submit(AssetRequestCreateVm model, string requestedByUserId);

        void Approve(int id, string reviewedByUserId, string notes);

        void Reject(int id, string reviewedByUserId, string notes);

        void Fulfill(int id, int assetId, string fulfilledByUserId, AssetAssignmentVm assignment);

        /// <summary>rel1009h: fulfil an approved request by receiving the asset into store (no assignee), like Purchases receive.</summary>
        void ReceiveIntoStore(int id, int assetId, string fulfilledByUserId, string notes);
    }
}
