using System.Linq;
using AssetManagement.Domain.Entities;

namespace AssetManagement.Application.Contracts
{
    public interface IDepartmentScopeService
    {
        bool BypassesDepartmentScope { get; }

        int? ScopedDepartmentId { get; }

        IQueryable<Asset> ApplyAssetScope(IQueryable<Asset> query);

        IQueryable<Department> ApplyDepartmentScope(IQueryable<Department> query);

        void EnsureCanAccessAsset(Asset asset);

        void EnsureCanAccessDepartment(Department department);

        void EnsureCanAccessDepartmentId(int departmentId);

        int CountVisibleDepartments(bool activeOnly = true);
    }
}
