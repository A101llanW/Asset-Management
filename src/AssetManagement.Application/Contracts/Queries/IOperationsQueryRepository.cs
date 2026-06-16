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

        bool ExistsActiveAssetTag(int organizationId, string assetTag);

        bool ExistsActiveSerialNumber(int organizationId, string serialNumber);
    }
}
