using System.Collections.Generic;
using AssetManagement.Application.DTOs;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IPendingApprovalQueryRepository
    {
        int CountGlobalPending(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool bypassPurchaseDepartmentScope,
            bool bypassAssetRequestDepartmentScope);

        IList<PendingApprovalSourceRow> GetPendingSources(
            int organizationId,
            int? departmentId,
            bool bypassDepartmentScope,
            bool denyDepartmentScope,
            bool bypassPurchaseDepartmentScope,
            bool bypassAssetRequestDepartmentScope);
    }
}
