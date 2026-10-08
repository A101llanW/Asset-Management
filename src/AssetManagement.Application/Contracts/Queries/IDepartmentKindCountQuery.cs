using AssetManagement.Application.DTOs;

namespace AssetManagement.Application.Contracts.Queries
{
    public interface IDepartmentKindCountQuery
    {
        /// <summary>One aggregate query: active counts by kind for an organisation (+ inactive total).</summary>
        DepartmentKindCountsDto GetCounts(int organizationId);
    }
}
