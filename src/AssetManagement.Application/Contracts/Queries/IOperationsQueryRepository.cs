using System.Collections.Generic;
using AssetManagement.Application.ViewModels;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IOperationsQueryRepository
    {
        IList<PurchaseRequestListItemVm> GetPurchaseRequestList(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope);

        PagedListVm<PurchaseRequestListItemVm> GetPurchaseRequestListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            string sort,
            string direction,
            int page,
            int pageSize);

        AssetRequestListPageVm GetAssetRequestListPage(
            AssetRequestFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool restrictToOwnDepartment);

        AssignmentListPageVm GetAssignmentListPage(
            AssignmentFilterVm filter,
            string sort,
            string direction,
            int page,
            int pageSize,
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope);

        IList<PurchaseRecordVm> GetPurchaseRecordList(int organizationId);

        PagedListVm<PurchaseRecordVm> GetPurchaseRecordListPage(
            int organizationId,
            string search,
            int? supplierId,
            string sort,
            string direction,
            int page,
            int pageSize);

        PagedListVm<IncidentListVm> GetIncidentListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            int? assetId,
            int page,
            int pageSize);

        PagedListVm<ClaimListVm> GetClaimListPage(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            string search,
            int? assetId,
            int page,
            int pageSize);

        bool ExistsActiveSerialNumber(int organizationId, string serialNumber, int? excludeAssetId = null);
    }
}
