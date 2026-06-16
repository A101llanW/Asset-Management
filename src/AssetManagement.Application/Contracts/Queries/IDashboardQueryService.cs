using AssetManagement.Application.DTOs;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IDashboardQueryService
    {
        DashboardKpisDto GetKpis(int organizationId, int? departmentId, bool bypassDepartmentScope, bool denyDepartmentScope);
    }
}
